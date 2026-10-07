using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class TubeNestHost {
 const string Origin="chrome-extension://fhjcedbbonnlkilcklhodjejolannode/";
 static readonly string Root=AppDomain.CurrentDomain.BaseDirectory,Tools=Path.Combine(Root,"tools");
 static readonly object Sync=new object(),WriteLock=new object();
 static readonly Stream Output=Console.OpenStandardOutput();
 static volatile bool closing;static Job active;static IntPtr processJob;
 internal sealed class Job { public string Id,Url,Title,Destination;public int Height;public double Duration;public volatile bool Cancelled;public Process Process;public bool Existing;public long ExistingLength;public DateTime ExistingTime; }
 static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=20000000,RecursionLimit=100};}
 static Dictionary<string,object> Obj(params object[] pairs){var d=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1];return d;}
 static string Text(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)&&v!=null?Convert.ToString(v,CultureInfo.InvariantCulture):"";}
 static double Number(Dictionary<string,object> d,string key){double v;return Double.TryParse(Text(d,key),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:0;}
 static bool Flag(Dictionary<string,object> d,string key){object v;return d!=null&&d.TryGetValue(key,out v)&&v is bool&&(bool)v;}
 internal static string VideoId(string value){Uri u;if(!Uri.TryCreate(value,UriKind.Absolute,out u)||u.Scheme!="https"||u.UserInfo.Length>0||!u.IsDefaultPort)throw new ArgumentException("只支援 YouTube HTTPS 影片網址。");string id=null;if(u.Host=="youtu.be")id=u.AbsolutePath.Trim('/');else if(new[]{"www.youtube.com","youtube.com","m.youtube.com"}.Contains(u.Host)){if(u.AbsolutePath=="/watch"){var m=Regex.Match(u.Query,@"(?:^\?|&)v=([\w-]{11})(?:&|$)");if(m.Success)id=m.Groups[1].Value;}else{var m=Regex.Match(u.AbsolutePath,@"^/shorts/([\w-]{11})/?$");if(m.Success)id=m.Groups[1].Value;}}if(id==null||!Regex.IsMatch(id,@"^[\w-]{11}$"))throw new ArgumentException("請使用 YouTube 單支影片或 Shorts 網址。");return id;}
 static string Canonical(string value){return "https://www.youtube.com/watch?v="+VideoId(value);}
 [DllImport("kernel32.dll")]static extern IntPtr CreateJobObject(IntPtr attributes,string name);
 [DllImport("kernel32.dll")]static extern bool SetInformationJobObject(IntPtr job,int type,IntPtr info,uint length);
 [DllImport("kernel32.dll")]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 [StructLayout(LayoutKind.Sequential)]struct BasicLimit{public long Time1,Time2;public uint Flags;public UIntPtr MinWorking,MaxWorking;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
 [StructLayout(LayoutKind.Sequential)]struct IoCounters{public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
 [StructLayout(LayoutKind.Sequential)]struct ExtendedLimit{public BasicLimit Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
 internal static void InitProcessJob(){processJob=CreateJobObject(IntPtr.Zero,null);if(processJob==IntPtr.Zero)throw new IOException("無法管理下載程序。");var limits=new ExtendedLimit();limits.Basic.Flags=0x2000;int size=Marshal.SizeOf(limits);IntPtr ptr=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(limits,ptr,false);if(!SetInformationJobObject(processJob,9,ptr,(uint)size))throw new IOException("無法設定下載程序。");}finally{Marshal.FreeHGlobal(ptr);}}
 [STAThread]public static int Main(string[] args){
   if(args.Length==0||args[0]!=Origin)return 2;
   try{InitProcessJob();var input=Console.OpenStandardInput();for(;;){var header=Read(input,4);if(header==null)break;uint length=BitConverter.ToUInt32(header,0);if(length==0||length>262144)break;var body=Read(input,(int)length);if(body==null)break;var request=Json().Deserialize<Dictionary<string,object>>(new UTF8Encoding(false,true).GetString(body));ThreadPool.QueueUserWorkItem(_=>Handle(request));}return 0;}
   catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
   finally{closing=true;lock(Sync){if(active!=null)active.Cancelled=true;}if(processJob!=IntPtr.Zero)CloseHandle(processJob);}
 }
 static byte[] Read(Stream stream,int size){var data=new byte[size];int at=0;while(at<size){int n=stream.Read(data,at,size-at);if(n==0)return null;at+=n;}return data;}
 static void Send(object value){if(closing)return;var data=Encoding.UTF8.GetBytes(Json().Serialize(value));if(data.Length>900000)throw new IOException("回應資料過大。");lock(WriteLock){if(closing)return;Output.Write(BitConverter.GetBytes(data.Length),0,4);Output.Write(data,0,data.Length);Output.Flush();}}
 static void Event(Job j,string status,double progress=0,string error="",object verification=null){Send(Obj("event","job","job",Obj("id",j.Id,"title",j.Title,"url",j.Url,"path",j.Destination,"status",status,"progress",progress,"error",error,"verification",verification)));}
 static void Handle(Dictionary<string,object> request){string id=Text(request,"id");try{object result;switch(Text(request,"action")){
   case "probe":lock(Sync){if(active!=null)throw new IOException("請等目前工作完成。");}result=Probe(Text(request,"url"));break;
   case "start":result=Start(request);break;
   case "cancel":lock(Sync){if(active!=null&&active.Id==Text(request,"jobId")){active.Cancelled=true;Kill(active.Process);}}result=Obj();break;
   case "reveal":string path=Text(request,"path");if(!Path.IsPathRooted(path)||!File.Exists(path)||Path.GetExtension(path).ToLowerInvariant()!=".mp4")throw new IOException("找不到影片檔案。");Process.Start(new ProcessStartInfo("explorer.exe","/select,"+Quote(path)){UseShellExecute=true});result=Obj();break;
   default:throw new ArgumentException("不支援的操作。");}
   Send(Obj("id",id,"ok",true,"data",result));
 }catch(Exception e){Send(Obj("id",id,"ok",false,"error",Friendly(e.Message)));}}
 internal static string Quote(string value){var s=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){s.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}s.Append('\\',slashes).Append(c);slashes=0;}return s.Append('\\',slashes*2).Append('"').ToString();}
 static void Kill(Process p){if(p==null)return;try{if(!p.HasExited){using(var k=Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"taskkill.exe"),"/PID "+p.Id+" /T /F"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){k.WaitForExit(5000);}if(!p.HasExited)p.Kill();}}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}
 static string Run(string tool,List<string> args,Job job=null,Action<string> line=null,int timeout=180000){
   if(!File.Exists(Path.Combine(Tools,tool)))throw new FileNotFoundException("缺少下載工具，請執行 Install.cmd。");
   var info=new ProcessStartInfo(Path.Combine(Tools,tool),String.Join(" ",args.Select(Quote))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Root};info.EnvironmentVariables["PATH"]=Tools+";"+Environment.GetEnvironmentVariable("PATH");
   using(var p=new Process{StartInfo=info}){
     lock(Sync){if(job!=null&&(job.Cancelled||closing))throw new OperationCanceledException();p.Start();p.StandardInput.Close();if(!AssignProcessToJobObject(processJob,p.Handle)){Kill(p);throw new IOException("無法管理下載程序。");}if(job!=null)job.Process=p;}
     var output=new StringBuilder();var errors=new StringBuilder();object capture=new object();Exception callbackError=null;bool overflow=false;
     p.OutputDataReceived+=(_,e)=>{if(e.Data==null)return;lock(capture){if(output.Length+e.Data.Length>19000000)overflow=true;else output.AppendLine(e.Data);try{if(line!=null)line(e.Data);}catch(Exception ex){callbackError=ex;Kill(p);}}};
     p.ErrorDataReceived+=(_,e)=>{if(e.Data==null)return;lock(capture){errors.AppendLine(e.Data);if(errors.Length>8000)errors.Remove(0,errors.Length-8000);}};
     p.BeginOutputReadLine();p.BeginErrorReadLine();if(!p.WaitForExit(timeout)){Kill(p);throw new TimeoutException("工作逾時，已中止。請重新讀取或更新下載工具。");}p.WaitForExit();
     if(job!=null){lock(Sync){if(job.Process==p)job.Process=null;}if(job.Cancelled||closing)throw new OperationCanceledException();}if(callbackError!=null)throw callbackError;if(overflow)throw new IOException("影片資料超過大小限制。");if(p.ExitCode!=0)throw new IOException(errors.ToString());return output.ToString();
   }
 }
 static List<string> Common(){return new List<string>{"--ignore-config","--no-plugin-dirs","--no-playlist","--encoding","utf-8","--socket-timeout","20","--retries","3","--fragment-retries","3","--ffmpeg-location",Tools,"--js-runtimes","deno:"+Path.Combine(Tools,"deno.exe")};}
 internal static object Probe(string url){url=Canonical(url);var args=Common();args.AddRange(new[]{"--dump-single-json","--skip-download","--",url});var data=Json().Deserialize<Dictionary<string,object>>(Run("yt-dlp.exe",args));if(Text(data,"id")!=VideoId(url)||Text(data,"extractor_key")!="Youtube")throw new IOException("影片來源不符。");if(Flag(data,"is_live")||Flag(data,"was_live"))throw new IOException("直播及直播回放暫不支援。");if(Number(data,"duration")<=0)throw new IOException("影片片長無法確認。");object raw;var sizes=new SortedDictionary<int,int>();if(data.TryGetValue("formats",out raw)&&raw is IList)foreach(Dictionary<string,object> f in (IList)raw){if(Text(f,"vcodec")=="none"||Flag(f,"has_drm"))continue;int h=(int)Number(f,"height"),w=(int)Number(f,"width");if(h>0&&w>0)sizes[h]=w;}if(sizes.Count==0)throw new IOException("沒有可下載的畫質。");return Obj("id",Text(data,"id"),"title",Text(data,"title"),"uploader",Text(data,"uploader"),"duration",Number(data,"duration"),"qualities",sizes.Reverse().Select(q=>Obj("height",q.Key,"width",q.Value)).ToArray());}
 static object Start(Dictionary<string,object> r){
   string url=Canonical(Text(r,"url")),jobId=Text(r,"jobId");Guid parsed;int height;if(!Guid.TryParse(jobId,out parsed)||!Int32.TryParse(Text(r,"quality"),out height)||height<100||height>16384)throw new ArgumentException("下载設定不正確。");object value;var expected=r.TryGetValue("expected",out value)?value as Dictionary<string,object>:null;if(expected==null||Text(expected,"id")!=VideoId(url)||Number(expected,"duration")<=0)throw new ArgumentException("請先讀取目前影片的畫質。");
   var job=new Job{Id=jobId,Url=url,Title=Text(expected,"title"),Duration=Number(expected,"duration"),Height=height};lock(Sync){if(active!=null)throw new IOException("同時只處理一支影片。");active=job;}
   try{job.Destination=Choose(job.Title);if(job.Destination==null){lock(Sync)active=null;return Obj("cancelled",true);}job.Existing=File.Exists(job.Destination);if(job.Existing){var f=new FileInfo(job.Destination);job.ExistingLength=f.Length;job.ExistingTime=f.LastWriteTimeUtc;}ThreadPool.QueueUserWorkItem(_=>Download(job));return Obj("cancelled",false,"id",job.Id);}
   catch{lock(Sync)active=null;throw;}
 }
 internal static string Choose(string title){string result=null;Exception error=null;var thread=new Thread(()=>{try{var safe=Regex.Replace(title,@"[<>:""/\\|?*\x00-\x1f]","_").Trim().TrimEnd('.');if(safe.Length>100)safe=safe.Substring(0,100);if(String.IsNullOrEmpty(safe)||Regex.IsMatch(safe,@"^(CON|PRN|AUX|NUL|COM\d|LPT\d)(\.|$)",RegexOptions.IgnoreCase))safe="TubeNest 影片";using(var owner=new Form{TopMost=true,ShowInTaskbar=false,Opacity=0})using(var dialog=new SaveFileDialog{Title="儲存 YouTube 影片",Filter="MP4 影片|*.mp4",DefaultExt="mp4",AddExtension=true,OverwritePrompt=true,FileName=safe+".mp4",InitialDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads")}){owner.Show();if(dialog.ShowDialog(owner)==DialogResult.OK)result=dialog.FileName;}}catch(Exception e){error=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;return result;}
 internal static void Download(Job job){string stage=null,finalStatus="failed",finalError="";object finalVerification=null;try{
   stage=Path.Combine(Path.GetDirectoryName(job.Destination),".TubeNest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);File.SetAttributes(stage,FileAttributes.Hidden);
   var args=Common();args.AddRange(new[]{"--no-simulate","--check-formats","--no-overwrites","--windows-filenames","--newline","--progress","--progress-delta","1","--match-filters","!is_live & !was_live","-f","bv[height="+job.Height+"]+ba/b[height="+job.Height+"]","-S","res,vcodec:h264,acodec:aac","--merge-output-format","mp4","--remux-video","mp4","-o",Path.Combine(stage,"media.%(ext)s"),"--print","before_dl:MD_META:%(.{id,extractor_key,title,duration,height})j","--print","after_move:MD_FILE:%(filepath)j","--progress-template","download:MD_PROGRESS:%(progress)j","--progress-template","postprocess:MD_MERGE:%(progress.status)s","--",job.Url});
   string file=null;Dictionary<string,object> metadata=null;Event(job,"downloading");
   Run("yt-dlp.exe",args,job,line=>{
     if(line.StartsWith("MD_META:")){metadata=Json().Deserialize<Dictionary<string,object>>(line.Substring(8));if(Text(metadata,"id")!=VideoId(job.Url)||Text(metadata,"extractor_key")!="Youtube"||(int)Number(metadata,"height")!=job.Height)throw new IOException("影片 ID 或畫質與選擇不符。");}
     else if(line.StartsWith("MD_FILE:"))file=Json().Deserialize<string>(line.Substring(8));
     else if(line.StartsWith("MD_PROGRESS:")){var p=Json().Deserialize<Dictionary<string,object>>(line.Substring(12));double total=Number(p,"total_bytes");if(total<=0)total=Number(p,"total_bytes_estimate");Event(job,"downloading",total>0?Math.Min(99,Number(p,"downloaded_bytes")/total*100):0);}
     else if(line.StartsWith("MD_MERGE:"))Event(job,"merging");
   },86400000);
   var expected=Path.Combine(stage,"media.mp4");if(metadata==null||file==null||!String.Equals(Path.GetFullPath(file),expected,StringComparison.OrdinalIgnoreCase)||!File.Exists(file))throw new IOException("沒有取得完整影片檔案。");
   Event(job,"verifying");var verification=Verify(job,file);if(job.Cancelled||closing)throw new OperationCanceledException();
   if(job.Existing){var target=new FileInfo(job.Destination);if(!target.Exists||target.Length!=job.ExistingLength||target.LastWriteTimeUtc!=job.ExistingTime)throw new IOException("目的檔案已變更，已停止覆蓋。");File.Replace(file,job.Destination,null);}else File.Move(file,job.Destination);
   finalStatus="completed";finalVerification=verification;
 }catch(OperationCanceledException){finalStatus="cancelled";}catch(Exception e){finalStatus=job.Cancelled?"cancelled":"failed";finalError=job.Cancelled?"":Friendly(e.Message);}
 finally{lock(Sync){if(active==job)active=null;}if(stage!=null){var root=Path.GetFullPath(Path.GetDirectoryName(job.Destination)).TrimEnd('\\')+"\\";var resolved=Path.GetFullPath(stage);if(resolved.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(Path.GetFileName(resolved),@"^\.TubeNest-[a-f0-9]{32}$"))try{Directory.Delete(resolved,true);}catch(IOException){}catch(UnauthorizedAccessException){}}Event(job,finalStatus,finalStatus=="completed"?100:0,finalError,finalVerification);}
 }
 internal static object Verify(Job job,string file){
   var data=Json().Deserialize<Dictionary<string,object>>(Run("ffprobe.exe",new List<string>{"-v","error","-show_entries","format=duration,size:stream=codec_type,width,height","-of","json",file},job));
   var format=(Dictionary<string,object>)data["format"];var streams=((IList)data["streams"]).Cast<Dictionary<string,object>>().ToArray();var video=streams.FirstOrDefault(s=>Text(s,"codec_type")=="video");double duration=Number(format,"duration"),tolerance=Math.Max(0.5,Math.Min(2,job.Duration*0.005));if(video==null||!streams.Any(s=>Text(s,"codec_type")=="audio")||(int)Number(video,"height")!=job.Height||Math.Abs(duration-job.Duration)>tolerance)throw new IOException("檔案的畫質、片長或影音軌道不完整。");
   double end=0;long frames=0;
   Run("ffmpeg.exe",new List<string>{"-hide_banner","-nostdin","-v","error","-xerror","-err_detect","explode","-protocol_whitelist","file,pipe","-threads","2","-i",file,"-map","0:v:0","-map","0:a:0","-threads","2","-progress","pipe:1","-nostats","-f","null","-"},job,line=>{if(line.StartsWith("out_time_us=")){double n;if(Double.TryParse(line.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out n)){end=Math.Max(end,n/1000000);Event(job,"verifying",Math.Min(99,end/duration*100));}}if(line.StartsWith("frame=")){long n;if(Int64.TryParse(line.Substring(6).Trim(),out n))frames=Math.Max(frames,n);}},86400000);
   if(frames==0||end<duration-tolerance)throw new IOException("影音沒有完整解碼至結尾。");string hash;using(var sha=SHA256.Create())using(var input=File.OpenRead(file)){hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
   return Obj("fullDecode",true,"identityMatched",true,"sourceId",VideoId(job.Url),"height",job.Height,"width",Number(video,"width"),"duration",duration,"frames",frames,"sha256",hash,"size",new FileInfo(file).Length);
 }
 static string Friendly(string value){string message=Regex.Replace(value??"",@"https?://\S+","[來源網址]").Trim();if(message.Length>1200)message=message.Substring(message.Length-1200);if(message.Contains("403")||message.IndexOf("Sign in",StringComparison.OrdinalIgnoreCase)>=0||message.IndexOf("attestation",StringComparison.OrdinalIgnoreCase)>=0)return "YouTube 拒絕下載或要求播放驗證。請更新工具後重試。\n"+message;return String.IsNullOrEmpty(message)?"本機助手處理失敗。":message;}
}
