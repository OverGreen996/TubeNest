using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

[assembly: AssemblyTitle("TubeNest 安裝精靈")]
[assembly: AssemblyDescription("TubeNest current-user setup")]
[assembly: AssemblyProduct("TubeNest")]
[assembly: AssemblyCompany("OverGreen996")]
public sealed class Setup {
 internal readonly Window Window;
 readonly JavaScriptSerializer json=new JavaScriptSerializer();
 readonly StringBuilder log=new StringBuilder();
 readonly string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TubeNestYouTube");
 readonly string logFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TubeNestYouTube","setup.log");
 string staging; Process worker; bool busy,ready,cancelled,verified,repair,refreshTools; string selectedBrowser,backendError;int issues;
 IntPtr processJob;
 T Find<T>(string name) where T:class {return Window.FindName(name) as T;}
 void Text(string name,string text){Find<TextBlock>(name).Text=text;}
 void Visible(string name,bool visible){Find<UIElement>(name).Visibility=visible?Visibility.Visible:Visibility.Collapsed;}
 internal Setup(){
  using(var x=Assembly.GetExecutingAssembly().GetManifestResourceStream("Setup.xaml")){Window=(Window)XamlReader.Load(x);}
  ApplyTheme();
  Window.Height=Math.Min(Window.Height,Math.Max(Window.MinHeight,SystemParameters.WorkArea.Height-32));
  using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Logo.png")){
   var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=s;image.EndInit();image.Freeze();Find<Image>("Logo").Source=image;Window.Icon=image;
  }
  Text("Version","v"+Version());
  foreach(string browser in new[]{"Chrome","Edge"}){
   bool installed=BrowserPath(browser)!=null;Find<RadioButton>(browser).IsEnabled=installed;
   Text(browser+"Hint",installed?"已偵測到瀏覽器":"此電腦尚未安裝");
  }
  if(Find<RadioButton>("Chrome").IsEnabled)Find<RadioButton>("Chrome").IsChecked=true;
  else if(Find<RadioButton>("Edge").IsEnabled)Find<RadioButton>("Edge").IsChecked=true;
  else {Find<Button>("Primary").IsEnabled=false;ShowError("請先安裝 Chrome 或 Edge，再重新開啟精靈。");}
  bool existing=File.Exists(Path.Combine(target,"extension","manifest.json"))||File.Exists(Path.Combine(target,"native","TubeNestHost.exe"));
  Text("DetectedInstall",existing?"偵測到 TubeNest，已預選檢查與修復模式。":"尚未偵測到 TubeNest，已預選標準安裝。");
  Find<RadioButton>("InstallMode").Checked+=(_,e)=>{Visible("UpdateTools",false);Find<Button>("Primary").Content="開始安裝";};
  Find<RadioButton>("RepairMode").Checked+=(_,e)=>{Visible("UpdateTools",true);Find<Button>("Primary").Content="檢查並修復";};
  Find<RadioButton>(existing?"RepairMode":"InstallMode").IsChecked=true;
  Find<Button>("Primary").Click+=async(_,e)=>{if(ready)OpenBrowser();else await Install();};
  Find<Button>("Secondary").Click+=(_,e)=>{if(busy)Cancel();else Window.Close();};
  Find<Button>("Copy").Click+=(_,e)=>CopyPath();
  Window.Closing+=(_,e)=>{if(busy){e.Cancel=true;Cancel();}};
 }
 static string Version(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Version.txt"))using(var r=new StreamReader(s)){return r.ReadToEnd().Trim();}}
 void ApplyTheme(){
  bool dark=false;using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){dark=k!=null&&Convert.ToInt32(k.GetValue("AppsUseLightTheme",1))==0;}
  if(!dark||SystemParameters.HighContrast)return;
  string[] names={"Surface","Card","Ink","Muted","Line","Accent","Tint","Error"};
  string[] colors={"#171B22","#212731","#F0F4FA","#B3BFCE","#414C5D","#A9C7FF","#27354B","#FFB4AB"};
  for(int i=0;i<names.Length;i++)Window.Resources[names[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
  // Dark-mode primary text needs contrast against the lighter accent.
  Find<Button>("Primary").Foreground=new SolidColorBrush(Color.FromRgb(20,38,68));
 }
 internal static string BrowserPath(string browser){
  return FindBrowser(browser,new[]{Environment.GetEnvironmentVariable("ProgramFiles"),Environment.GetEnvironmentVariable("ProgramFiles(x86)"),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)});
 }
 internal static string FindBrowser(string browser,IEnumerable<string> roots){
  string relative=browser=="Chrome"?@"Google\Chrome\Application\chrome.exe":@"Microsoft\Edge\Application\msedge.exe";
  foreach(string root in roots){
   if(!String.IsNullOrEmpty(root)){string path=Path.Combine(root,relative);if(File.Exists(path))return path;}
  }
  return null;
 }
 internal static string Quote(string value){
  var s=new StringBuilder("\"");int slashes=0;
  foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){s.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}s.Append('\\',slashes).Append(c);slashes=0;}
  return s.Append('\\',slashes*2).Append('"').ToString();
 }
 internal static void Extract(Stream payload,string directory){
  string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  using(var archive=new ZipArchive(payload,ZipArchiveMode.Read)){
   if(archive.Entries.Count>2000)throw new IOException("安裝檔內容異常。");
   long size=0;
   foreach(var entry in archive.Entries){
    string path=Path.GetFullPath(Path.Combine(root,entry.FullName));
    if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||entry.FullName.Contains(":"))throw new IOException("安裝檔路徑無效。");
    size+=entry.Length;if(size>100*1024*1024)throw new IOException("安裝檔過大。");
    if(String.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(path);continue;}
    Directory.CreateDirectory(Path.GetDirectoryName(path));using(var source=entry.Open())using(var dest=new FileStream(path,FileMode.CreateNew,FileAccess.Write)){source.CopyTo(dest);}
   }
  }
 }
 void ShowError(string message){Visible("ErrorPanel",true);Text("ErrorText",message);}
 void AddLog(string line){
  log.AppendLine(line);if(log.Length>48000)log.Remove(0,log.Length-48000);
  Find<TextBox>("Log").Text=log.ToString();Find<TextBox>("Log").ScrollToEnd();
 }
 internal void Progress(string line){
  if(!line.StartsWith("@TN:",StringComparison.Ordinal)){AddLog(line);return;}
  Dictionary<string,object> data;
  try{data=json.Deserialize<Dictionary<string,object>>(line.Substring(4));}catch{AddLog(line);return;}
  string stage=Convert.ToString(data["stage"]),message=Convert.ToString(data["message"]);
  if(stage=="error"){backendError=message;AddLog("安裝失敗："+message);return;}
  bool download=stage=="download";
  string friendly=message=="yt-dlp.exe"?"下載影片讀取工具":message=="ffmpeg-master-latest-win64-gpl.zip"?"下載影音處理工具":message=="deno-x86_64-pc-windows-msvc.zip"?"下載播放解析工具":message;
  friendly=friendly.Replace("ffmpeg-master-latest-win64-gpl.zip","影音處理工具").Replace("deno-x86_64-pc-windows-msvc.zip","播放解析工具").Replace("yt-dlp.exe","影片讀取工具");
  Text("Current",friendly);var bar=Find<ProgressBar>("Progress");
  double received=Convert.ToDouble(data["received"]),total=Convert.ToDouble(data["total"]),speed=Convert.ToDouble(data["speed"]);
  bar.IsIndeterminate=!download||total<=0;
  if(download){
   double percent=total>0?Math.Min(100,received/total*100):0;bar.Value=percent;
   Text("Percent",total>0?Math.Floor(percent)+"%":"");
   Text("Transfer",Bytes(received)+(total>0?" / "+Bytes(total):" 已下載")+"  ·  "+Bytes(speed)+"/s");
  }else{Text("Percent","");Text("Transfer",stage=="reuse"?"已檢查工具可正常執行，略過下載。":stage=="verify"?"確認檔案與官方發行版本一致。":stage=="extract"?"正在解壓縮，請稍候…":"正在處理，請稍候…");AddLog(message);}
  if(stage=="diagnose")return;
  if(stage!="prepare")Text("PrepareRow","✓  準備安裝檔案");
  if(stage=="register"||stage=="check"||stage=="ready"){Text("DownloadRow","✓  下載與驗證官方元件");Text("ConnectRow","●  連接並檢查本機助手");}
  else if(stage!="prepare")Text("DownloadRow","●  下載與驗證官方元件");
  if(stage=="ready"){verified=true;Text("ConnectRow","✓  連接並檢查本機助手");}
 }
 internal static string Bytes(double value){return value>=1073741824?(value/1073741824).ToString("0.00")+" GB":value>=1048576?(value/1048576).ToString("0.0")+" MB":value>=1024?(value/1024).ToString("0.0")+" KB":Math.Max(0,value).ToString("0")+" B";}
 async Task Install(){
  if(busy)return;
  selectedBrowser=Find<RadioButton>("Chrome").IsChecked==true?"Chrome":"Edge";
  if(BrowserPath(selectedBrowser)==null){ShowError("找不到選擇的瀏覽器，請先完成瀏覽器安裝。");return;}
  busy=true;cancelled=false;verified=false;ready=false;backendError=null;log.Clear();Find<TextBox>("Log").Clear();
  repair=Find<RadioButton>("RepairMode").IsChecked==true;refreshTools=repair&&Find<CheckBox>("UpdateTools").IsChecked==true;issues=0;
  Visible("Welcome",false);Visible("Ready",false);Visible("ErrorPanel",false);Visible("Installing",true);Visible("Details",true);
  Find<Button>("Primary").IsEnabled=false;Find<Button>("Secondary").Content="取消安裝";
  Text("Title",repair?"正在檢查並修復 TubeNest":"正在準備你的下載工具");Text("Subtitle",repair?"檢查擴充檔案、工具與瀏覽器連接，修復缺漏或損壞的元件。":"安裝完成後，只剩瀏覽器的擴充確認。這個視窗會持續顯示進度。");Text("Eyebrow","第 2 步，共 3 步");Text("Step1","✓    選擇瀏覽器");
  Text("Footer","安裝期間需保持網路連線");Text("LogPath","紀錄位置："+logFile);
  try{
   await Task.Run(()=>RunBackend());
   if(cancelled)throw new OperationCanceledException();
   if(!verified)throw new IOException("安裝未通過助手檢查。請查看詳細資訊後重試。");
   ShowReady();
  }catch(Exception e){
   Text("Title",cancelled?"安裝已取消":"這次安裝未完成");Text("Subtitle","已下載完成的工具會保留；重試時會先檢查並沿用。");
   ShowError(cancelled?"你可以重新開始安裝。":(backendError??e.Message)+"\n請確認網路可連線至 GitHub，再按「重試安裝」。");
   AddLog(e.ToString());Find<Button>("Primary").Content="重試安裝";
  }finally{
   busy=false;worker=null;Find<Button>("Primary").IsEnabled=true;Find<Button>("Secondary").IsEnabled=true;Find<Button>("Secondary").Content="關閉";
   try{Directory.CreateDirectory(target);File.WriteAllText(logFile,log.ToString(),new UTF8Encoding(false));}catch{}
  }
 }
 void RunBackend(){
  staging=Path.Combine(Path.GetTempPath(),"TubeNest-Setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
  try{
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip")){Extract(s,staging);}
   // Keep a reusable repair entry point alongside the installed application.
   File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(staging,"TubeNest-Setup.exe"));
   if(cancelled)throw new OperationCanceledException();
   string script=Path.Combine(staging,"scripts","Install.ps1");if(!File.Exists(script))throw new IOException("安裝檔缺少必要內容。");
   processJob=CreateKillJob();
   if(repair)InspectInstallation();
   if(cancelled)throw new OperationCanceledException();
   var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe"),"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(script)+" -Wizard -NoBrowser -TargetRoot "+Quote(target)+" -WorkRoot "+Quote(staging)+(refreshTools?" -RefreshTools":"")){
    UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=staging};
   using(var process=new Process{StartInfo=info}){
    worker=process;
    process.OutputDataReceived+=(_,e)=>{if(e.Data!=null)Window.Dispatcher.BeginInvoke(new Action(()=>Progress(e.Data)));};
    process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)Window.Dispatcher.BeginInvoke(new Action(()=>AddLog(e.Data)));};
    process.Start();
    if(!AssignProcessToJobObject(processJob,process.Handle)){try{process.Kill();}catch{};throw new IOException("無法管理安裝程序。");}
    process.BeginOutputReadLine();process.BeginErrorReadLine();
    if(cancelled)StopWorker();
    process.WaitForExit();if(cancelled)throw new OperationCanceledException();
    if(process.ExitCode!=0)throw new IOException("元件安裝或驗證失敗。詳細原因已列在「安裝詳細資訊」。");
   }
  }finally{
   if(processJob!=IntPtr.Zero){CloseHandle(processJob);processJob=IntPtr.Zero;}
   string path=Path.GetFullPath(staging),temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\')+"\\";
   if(path.StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),"^TubeNest-Setup-[a-f0-9]{32}$")){try{Directory.Delete(path,true);}catch{}}
  }
 }
 internal void ShowReady(){
  ready=true;Visible("Installing",false);Visible("Ready",true);
  Text("Title","最後一步，交給瀏覽器確認");Text("Subtitle",repair?"修復完成。請在擴充管理頁重新載入 TubeNest，再重新整理 YouTube。":"助手安裝完成。載入擴充後，就能在 YouTube 選畫質與儲存位置。");Text("Eyebrow","第 3 步，共 3 步");
  Text("Step2","✓    下載並安裝");Text("Footer","本機助手安裝完成 · 擴充待確認");
  Find<TextBox>("Folder").Text=Path.Combine(target,"extension");
  Text("LoadInstruction",selectedBrowser=="Chrome"?"2   按「載入未封裝項目」（Load unpacked）":"2   按「載入解壓縮」（Load unpacked）");
  Find<Button>("Primary").Content="開啟 "+(selectedBrowser??"Edge")+" 擴充頁";
 }
 void CopyPath(){try{Clipboard.SetText(Path.Combine(target,"extension"));Text("CopyHint","資料夾路徑已複製。");}catch{Text("CopyHint","無法複製，請選取上方路徑手動複製。");}}
 void OpenBrowser(){
  string browser=BrowserPath(selectedBrowser);if(browser==null){ShowError("找不到瀏覽器，請手動開啟擴充管理頁。");return;}
  CopyPath();
  try{Process.Start(new ProcessStartInfo(browser,selectedBrowser=="Chrome"?"chrome://extensions":"edge://extensions"){UseShellExecute=true});Text("Subtitle","擴充管理頁已開啟。照下方三個步驟完成，然後重新整理 YouTube。");}
  catch(Exception e){ShowError("無法開啟瀏覽器："+e.Message);}
 }
 void Cancel(){
  if(cancelled)return;
  if(MessageBox.Show(Window,"要取消這次安裝嗎？已完成的元件會保留，之後可以重試。","TubeNest",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  cancelled=true;Text("Current","正在取消安裝…");Find<Button>("Secondary").IsEnabled=false;StopWorker();
 }
 void StopWorker(){try{if(worker!=null&&!worker.HasExited)worker.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}
 void Diagnostic(string message,bool ok){
  if(!ok)issues++;
  string text=(ok?"檢查通過：":"需要修復：")+message;
  Window.Dispatcher.BeginInvoke(new Action(()=>{Text("Current","檢查已安裝的 TubeNest");Text("Transfer",text);Find<ProgressBar>("Progress").IsIndeterminate=true;AddLog(text);}));
 }
 internal void InspectInstallation(){
  foreach(string relative in new[]{"extension","native"}){
   bool match=true;
   foreach(string expected in Directory.GetFiles(Path.Combine(staging,relative),"*",SearchOption.AllDirectories)){
    string actual=Path.Combine(target,expected.Substring(staging.Length+1));
    if(!File.Exists(actual)||!SameFile(expected,actual)){match=false;break;}
   }
   Diagnostic(relative=="extension"?"擴充檔案與版本":"本機助手檔案",match);
  }
  foreach(string name in new[]{"yt-dlp.exe","ffmpeg.exe","ffprobe.exe","deno.exe"}){
   if(cancelled)throw new OperationCanceledException();
   string path=Path.Combine(target,"native","tools",name);bool usable=false;
   if(File.Exists(path)){
    try{using(var p=new Process{StartInfo=new ProcessStartInfo(path,name.StartsWith("ff")?"-version":"--version"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}}){
     worker=p;p.Start();if(!AssignProcessToJobObject(processJob,p.Handle)){p.Kill();throw new IOException("無法管理檢查程序。");}
     p.BeginOutputReadLine();p.BeginErrorReadLine();if(!p.WaitForExit(10000)){p.Kill();p.WaitForExit();}else usable=p.ExitCode==0;
    }}catch{usable=false;}finally{worker=null;}
   }
   Diagnostic(name+" 可執行",usable);
  }
  foreach(string browser in new[]{"Chrome","Edge"}){
   if(BrowserPath(browser)==null)continue;bool correct=false;
   string manifest=Path.Combine(target,"native","com.tubenest.youtube.json");
   using(var key=Registry.CurrentUser.OpenSubKey(@"Software\"+(browser=="Chrome"?@"Google\Chrome":@"Microsoft\Edge")+@"\NativeMessagingHosts\com.tubenest.youtube")){
    try{
     var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(manifest));
     var origins=data["allowed_origins"] as System.Collections.IList;
     correct=key!=null&&String.Equals(Convert.ToString(key.GetValue(null)),manifest,StringComparison.OrdinalIgnoreCase)
       &&Convert.ToString(data["name"])=="com.tubenest.youtube"&&Convert.ToString(data["type"])=="stdio"
       &&String.Equals(Convert.ToString(data["path"]),Path.Combine(target,"native","TubeNestHost.exe"),StringComparison.OrdinalIgnoreCase)
       &&origins!=null&&origins.Count==1&&Convert.ToString(origins[0])=="chrome-extension://fhjcedbbonnlkilcklhodjejolannode/";
    }catch{correct=false;}
   }
   Diagnostic(browser+" 助手連接",correct);
  }
  Window.Dispatcher.BeginInvoke(new Action(()=>AddLog("檢查結束："+issues+" 項需更新或修復。正在重建檔案與助手連接。瀏覽器是否已載入擴充需在擴充頁確認。")));
 }
 static bool SameFile(string left,string right){
  using(var sha=System.Security.Cryptography.SHA256.Create())using(var a=File.OpenRead(left))using(var b=File.OpenRead(right)){
   return Convert.ToBase64String(sha.ComputeHash(a))==Convert.ToBase64String(sha.ComputeHash(b));
  }
 }
 // A kill-on-close job keeps hidden installer children from outliving the wizard.
 [StructLayout(LayoutKind.Sequential)] struct BasicLimit {public long PerProcessUserTimeLimit,PerJobUserTimeLimit;public uint LimitFlags;public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
 [StructLayout(LayoutKind.Sequential)] struct IoCounters {public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
 [StructLayout(LayoutKind.Sequential)] struct ExtendedLimit {public BasicLimit BasicLimitInformation;public IoCounters IoInfo;public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr security,string name);
 [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job,int info,IntPtr data,uint length);
 [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 static IntPtr CreateKillJob(){
  IntPtr job=CreateJobObject(IntPtr.Zero,null);if(job==IntPtr.Zero)throw new IOException("無法建立安裝程序管理。");
  var limits=new ExtendedLimit();limits.BasicLimitInformation.LimitFlags=0x2000;int size=Marshal.SizeOf(limits);IntPtr memory=Marshal.AllocHGlobal(size);
  try{Marshal.StructureToPtr(limits,memory,false);if(!SetInformationJobObject(job,9,memory,(uint)size)){CloseHandle(job);throw new IOException("無法設定安裝程序管理。");}}finally{Marshal.FreeHGlobal(memory);}return job;
 }
 [STAThread] public static int Main(){
  bool first;using(var mutex=new Mutex(true,"Local\\TubeNest-Setup-CurrentUser",out first)){
   if(!first){MessageBox.Show("TubeNest 安裝精靈已開啟，請回到原本的安裝視窗。","TubeNest");return 1;}
   try{var app=new Application();var setup=new Setup();app.Run(setup.Window);return 0;}
   catch(Exception e){MessageBox.Show("無法啟動安裝精靈："+e.Message,"TubeNest",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
  }
 }
}
