using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

// Compiled with the real wizard source and embedded payload, never shipped.
public static class SetupHarness {
 static string root;static Setup setup;static Application app;static int snapshots;static bool cancelOnDownload;
 static readonly List<string> results=new List<string>();static readonly List<string> transfers=new List<string>();
 static T Control<T>(string name)where T:class{return setup.Window.FindName(name)as T;}
 static void Field(string name,object value){typeof(Setup).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(setup,value);}
 static object Field(string name){return typeof(Setup).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(setup);}
 static async Task Install(){await (Task)typeof(Setup).GetMethod("Install",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(setup,null);}
 static void Assert(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);results.Add(label);Console.WriteLine("PASS: "+label);}
 static void Capture(string name){
  setup.Window.UpdateLayout();var visual=(FrameworkElement)setup.Window.Content;
  var image=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(visual);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var s=File.Create(Path.Combine(root,name+".png")))encoder.Save(s);
 }
 static void Tick(object sender,EventArgs e){
  string current=Control<TextBlock>("Current").Text,percent=Control<TextBlock>("Percent").Text;
  if(current.StartsWith("下載")&&percent.Length>0&&Control<ProgressBar>("Progress").Value>0){
   string transfer=current+" "+percent+" "+Control<TextBlock>("Transfer").Text;
   if(transfers.Count==0||!transfers[transfers.Count-1].StartsWith(current))transfers.Add(transfer);
   if(snapshots==0){Capture("wizard-download");snapshots++;}
   if(cancelOnDownload){cancelOnDownload=false;Field("cancelled",true);typeof(Setup).GetMethod("StopWorker",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(setup,null);}
  }
 }
 [STAThread]public static int Main(string[] args){
  root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
  bool smoke=args.Length>1;
  app=new Application();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  setup=new Setup();Field("target",Path.Combine(root,"installed"));Field("logFile",Path.Combine(root,"setup.log"));
  setup.Window.Show();
  app.Dispatcher.BeginInvoke(new Action(async()=>{
   string[] keys={@"Software\Google\Chrome\NativeMessagingHosts\com.tubenest.youtube",@"Software\Microsoft\Edge\NativeMessagingHosts\com.tubenest.youtube"};
   var old=new Dictionary<string,object>();foreach(string path in keys){using(var k=Registry.CurrentUser.OpenSubKey(path)){old[path]=k==null?null:k.GetValue(null);}}
   var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};timer.Tick+=Tick;timer.Start();
   try{
    string install=Path.Combine(root,"installed");
    if(smoke){string tools=Path.Combine(install,"native","tools");Directory.CreateDirectory(tools);foreach(string name in new[]{"yt-dlp.exe","ffmpeg.exe","ffprobe.exe","deno.exe"})File.Copy(Path.Combine(args[1],"native","tools",name),Path.Combine(tools,name),true);}
    Control<RadioButton>("InstallMode").IsChecked=true;
    Control<TextBlock>("DetectedInstall").Text="尚未偵測到 TubeNest，已預選標準安裝。";
    Capture("wizard-welcome");
    Control<RadioButton>("RepairMode").IsChecked=true;Capture("wizard-repair");Control<RadioButton>("InstallMode").IsChecked=true;
    string[] brushNames={"Surface","Card","Ink","Muted","Line","Accent","Tint","Error"};string[] brushColors={"#F7F9FC","#FFFFFF","#192533","#526174","#D5DDE8","#185ABD","#EAF1FD","#B3261E"};var original=new Dictionary<string,object>();
    foreach(string name in brushNames)original[name]=setup.Window.Resources[name];var originalPrimary=Control<Button>("Primary").Foreground;
    for(int i=0;i<brushNames.Length;i++)setup.Window.Resources[brushNames[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(brushColors[i]));Control<Button>("Primary").Foreground=Brushes.White;Capture("wizard-welcome-light");
    foreach(string name in brushNames)setup.Window.Resources[name]=original[name];Control<Button>("Primary").Foreground=originalPrimary;
    Assert(Setup.BrowserPath("Edge")!=null,"Installed Edge detected");
    Assert(Setup.BrowserPath("Chrome")==null,"Absent standard Chrome detected (portable test browser excluded)");
    string mock=Path.Combine(root,"browser-path-test");Directory.CreateDirectory(Path.Combine(mock,@"Google\Chrome\Application"));Directory.CreateDirectory(Path.Combine(mock,@"Microsoft\Edge\Application"));
    File.WriteAllText(Path.Combine(mock,@"Google\Chrome\Application\chrome.exe"),"");File.WriteAllText(Path.Combine(mock,@"Microsoft\Edge\Application\msedge.exe"),"");
    Assert(Setup.FindBrowser("Chrome",new[]{mock})==Path.Combine(mock,@"Google\Chrome\Application\chrome.exe")&&Setup.FindBrowser("Edge",new[]{mock})==Path.Combine(mock,@"Microsoft\Edge\Application\msedge.exe"),"Both-browser path detection resolves Chrome and Edge independently");
    using(var memory=new MemoryStream()){
     using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)){var entry=zip.CreateEntry("../escaped.txt");using(var writer=new StreamWriter(entry.Open()))writer.Write("escape");}
     memory.Position=0;bool rejected=false;try{Setup.Extract(memory,Path.Combine(root,"extract-check"));}catch(IOException){rejected=true;}
     Assert(rejected&&!File.Exists(Path.Combine(root,"escaped.txt")),"Archive traversal rejected");
    }
    await Install();
    Assert((bool)Field("ready"),"Fresh official download/install/native handshake passed");
    if(!smoke)Assert(transfers.Count>=3,"Real byte progress observed for all three official downloads");else Assert(Control<TextBox>("Log").Text.Contains("沿用已安裝"),"Valid tools reused without downloading again");
    Assert(Control<TextBlock>("Footer").Text.Contains("擴充待確認"),"Browser extension confirmation is not falsely reported complete");
    Capture("wizard-ready");
    string blocked=Path.Combine(root,"blocked-target");File.WriteAllText(blocked,"not a directory");Field("target",blocked);await Install();
    Assert(!(bool)Field("ready")&&Control<Border>("ErrorPanel").Visibility==Visibility.Visible&&Control<TextBlock>("ErrorText").Text.Contains("重試安裝"),"Actual installation failure shows error and retry instead of false success");Capture("wizard-error");Field("target",install);
    File.AppendAllText(Path.Combine(install,"extension","youtube-button.js"),"\n// damaged test copy");
    File.WriteAllText(Path.Combine(install,"native","tools","deno.exe"),"broken test binary");
    using(var k=Registry.CurrentUser.CreateSubKey(keys[1]))k.SetValue("",Path.Combine(root,"missing.json"));
    Control<RadioButton>("RepairMode").IsChecked=true;
    await Install();
    Assert((bool)Field("ready")&&(int)Field("issues")>=3,"Repair detects extension damage, corrupt tool, and wrong Edge registration");
    string repairLog=Control<TextBox>("Log").Text;File.WriteAllText(Path.Combine(root,"repair.log"),repairLog);
    Assert(repairLog.Contains("需要修復：deno.exe")&&repairLog.Contains("需要修復：Edge"),"Repair reports the actual broken components");
    Assert(File.ReadAllText(Path.Combine(install,"extension","youtube-button.js"))==File.ReadAllText(Path.Combine(root,"..","..","extension","youtube-button.js")),"Damaged extension replaced with release bytes");
    await Install();
    Assert((bool)Field("ready")&&(int)Field("issues")==0,"Repaired installation passes repeat diagnostics with no issues");
    Control<CheckBox>("UpdateTools").IsChecked=true;cancelOnDownload=true;
    await Install();
    Assert(!(bool)Field("ready")&&Control<TextBlock>("Title").Text=="安裝已取消","Cancellation stops hidden installer and never reports success");
    Capture("wizard-cancelled");
    Control<CheckBox>("UpdateTools").IsChecked=false;
    await Install();
    Assert((bool)Field("ready"),"Retry after cancellation reuses verified tools and succeeds");
    string staging=(string)Field("staging");Assert(!Directory.Exists(staging),"Wizard temporary extraction/download files cleaned");
    File.WriteAllText(Path.Combine(root,"result.txt"),String.Join(Environment.NewLine,results)+Environment.NewLine+String.Join(Environment.NewLine,transfers));
    app.Shutdown(0);
   }catch(Exception e){Console.Error.WriteLine(e);File.WriteAllText(Path.Combine(root,"failure.txt"),e.ToString()+"\n"+Control<TextBox>("Log").Text);Capture("wizard-failure");app.Shutdown(1);}
   finally{
    timer.Stop();foreach(string path in keys){if(old[path]==null)Registry.CurrentUser.DeleteSubKey(path,false);else using(var k=Registry.CurrentUser.CreateSubKey(path))k.SetValue("",old[path]);}
   }
  }));
  return app.Run();
 }
}
