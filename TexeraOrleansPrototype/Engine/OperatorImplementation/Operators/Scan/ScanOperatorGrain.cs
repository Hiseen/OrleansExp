using Orleans;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Orleans.Concurrency;
using Engine.OperatorImplementation.Common;
using TexeraUtilities;
using Orleans.Runtime;

namespace Engine.OperatorImplementation.Operators
{
    public class ScanOperatorGrain : WorkerGrain, IScanOperatorGrain
    {
        private bool restarted=false;
        private double checkpoint=0.1;
        private ulong size=0,original_start=0;
        private ulong start,end,tuple_counter=0;
        private ScanStreamReader reader;
        public static int GenerateLimit=1000;
        private string separator;
        TimeSpan splitingTime=new TimeSpan(0,0,0);
        TimeSpan addingToListTime=new TimeSpan(0,0,0);
        TimeSpan generateTime=new TimeSpan(0,0,0);
        TimeSpan readtupleTime=new TimeSpan(0,0,0);

        protected override void Start()
        {
            base.Start();
        }

        protected override void Resume()
        {
            restarted=true;
            base.Resume();
            if(!isFinished)
            {
                Task.Run(()=>Generate());
            }
        }

        protected override async Task<List<TexeraTuple>> GenerateTuples()
        {
            DateTime start1=DateTime.UtcNow;
            List<TexeraTuple> outputList=new List<TexeraTuple>();
            for(int i=0;i<GenerateLimit;++i)
            {
                TexeraTuple tuple=await ReadTuple();
                DateTime start2=DateTime.UtcNow;
                if(tuple!=null)
                {
                    outputList.Add(tuple);
                }
                if(isPaused)
                {
                    return outputList;
                }
                if(start>end || reader.IsEOF())
                {
                    reader.Close();
                    Console.WriteLine(Common.Utils.GetReadableName(self)+" Spliting Time: "+splitingTime +" Adding to list Time: "+addingToListTime+" Generate Time: "+generateTime+" ReadTuple Time: "+readtupleTime);
                    reader.PrintTimeUsage(Common.Utils.GetReadableName(self));
                    currentEndFlagCount=0;
                    return outputList;
                }
                addingToListTime+=DateTime.UtcNow-start2;
            }
            generateTime+=DateTime.UtcNow-start1;
            return outputList;
        }

        

        public async override Task<SiloAddress> Init(IWorkerGrain self, PredicateBase predicate, IPrincipalGrain principalGrain)
        {
            SiloAddress addr=await base.Init(self,predicate,principalGrain);
            ulong filesize=((ScanPredicate)predicate).FileSize;
            separator=((ScanPredicate)predicate).Separator;
            string extensionKey = "";
            Guid key = this.GetPrimaryKey(out extensionKey);
            ulong i=UInt64.Parse(extensionKey);
            ulong num_grains=(ulong)((ScanPredicate)predicate).NumberOfGrains;
            ulong partition=filesize/num_grains;
            ulong start_byte=i*partition;
            ulong end_byte=num_grains-1==i?filesize:(i+1)*partition;
            reader=new ScanStreamReader(((ScanPredicate)predicate).File);
            if(!reader.GetFile(start_byte))
                throw new Exception("unable to get file");
            start=start_byte;
            end=end_byte;
            size=partition;
            original_start=start;
            if(start!=0)
                start+=await reader.TrySkipFirst();
            //Console.WriteLine("Init: start byte: "+start.ToString()+" end byte: "+end.ToString());
            return addr;
        }

        


        private async Task<TexeraTuple> ReadTuple()
        {
            DateTime start2=DateTime.UtcNow;
            // try
            // {
            Tuple<string,ulong> res = await reader.ReadLine();
            readtupleTime+=DateTime.UtcNow-start2;
            DateTime start1=DateTime.UtcNow;
            start += res.Item2;
            if (reader.IsEOF())
            {
                start = end + 1;
                return null;
            }
            try
            {
                ++tuple_counter;
                if(separator!=null)
                {
                    var fields=res.Item1.Split(separator);
                    splitingTime+=DateTime.UtcNow-start1;
                    return new TexeraTuple(fields);
                }
                else
                    return new TexeraTuple(new string[]{res.Item1});
            }
            catch
            {
                Console.WriteLine("Failed to parse the tuple");
                return null;
            }
            // }
            // catch(Exception ex)
            // {
            //     Console.WriteLine("EXCEPTION: in Reading Tuples from File - "+ ex.ToString());
            //     Console.WriteLine("start_offset: "+start.ToString()+" end_offset: "+end.ToString());
            //     int retry=0;
            //     while(retry<10 && !reader.GetFile(start))
            //     {
            //         retry++;
            //         Console.WriteLine("Cannot recover file on {retry} trial, will try again in 5 seconds");
            //         Thread.Sleep(5000);
            //     };
            //     return null;
            // }
        }
        
    }
}