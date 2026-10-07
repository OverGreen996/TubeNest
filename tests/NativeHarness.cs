using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
class NativeHarness {
 static int Main(string[] args){
  var type=Assembly.LoadFile(Path.GetFullPath(args[0])).GetType("TubeNestHost");var flags=BindingFlags.NonPublic|BindingFlags.Static;
  type.GetMethod("InitProcessJob",flags).Invoke(null,null);
  if(args[1]=="verify"){
   var j=Activator.CreateInstance(type.GetNestedType("Job",BindingFlags.NonPublic),true);
   foreach(var pair in new Dictionary<string,object>{{"Id",Guid.NewGuid().ToString()},{"Url",args[3]},{"Height",Int32.Parse(args[4])},{"Duration",Double.Parse(args[5],System.Globalization.CultureInfo.InvariantCulture)}})j.GetType().GetField(pair.Key).SetValue(j,pair.Value);
   try{var result=type.GetMethod("Verify",flags).Invoke(null,new[]{j,(object)args[2]});Console.Error.WriteLine(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result));return 0;}catch(TargetInvocationException e){Console.Error.WriteLine(e.InnerException.Message);return 3;}
  }
  var info=type.GetMethod("Probe",flags).Invoke(null,new object[]{args[3]}) as Dictionary<string,object>;
  var job=Activator.CreateInstance(type.GetNestedType("Job",BindingFlags.NonPublic),true);
  foreach(var pair in new Dictionary<string,object>{{"Id",Guid.NewGuid().ToString()},{"Url",args[3]},{"Title",info["title"]},{"Height",Int32.Parse(args[4])},{"Duration",info["duration"]},{"Destination",Path.GetFullPath(args[2])}})job.GetType().GetField(pair.Key).SetValue(job,pair.Value);
  if(args[1]=="cancel"){
   type.GetField("active",flags).SetValue(null,job);
   var thread=new Thread(()=>type.GetMethod("Download",flags).Invoke(null,new[]{job}));thread.Start();Thread.Sleep(250);
   type.GetMethod("Handle",flags).Invoke(null,new object[]{new Dictionary<string,object>{{"id",Guid.NewGuid().ToString()},{"action","cancel"},{"jobId",job.GetType().GetField("Id").GetValue(job)}}});thread.Join(30000);return !thread.IsAlive&&!File.Exists(args[2])?0:1;
  }
  type.GetMethod("Download",flags).Invoke(null,new[]{job});return File.Exists(args[2])?0:1;
 }
}
