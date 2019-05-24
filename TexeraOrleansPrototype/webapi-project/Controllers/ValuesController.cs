﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Engine.WorkflowImplementation;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Orleans;
using Orleans.Hosting;
using OrleansClient;

namespace webapi.Controllers
{
    //[Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private static IClusterClient client;
        public ValuesController()
        {
            
            if(client == null)
            {
                client = ClientWrapper.Instance.client;
            }
        }

        //Post api/pause
        [HttpPost]
        [Route("api/pause")]
        public async Task<HttpResponseMessage> PostPause()
        {
            Console.WriteLine("action: pause");
            Stopwatch sw=new Stopwatch();
            Stream req = Request.Body;
            string json = new StreamReader(req).ReadToEnd();
            JObject o = JObject.Parse(json);
            Guid workflowID;
            if(!Guid.TryParse(o["workflowID"].ToString().Substring(16),out workflowID))
            {
                throw new Exception($"Parse workflowID failed! For {o["workflowID"].ToString().Substring(16)}");
            }
            Console.WriteLine("target: "+workflowID);
            try
            {
                sw.Start();
                await ClientWrapper.Instance.PauseWorkflow(workflowID);
                sw.Stop();
                Console.WriteLine("Paused! Time Taken: "+sw.Elapsed);
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
            return new HttpResponseMessage();
        }


        //Post api/pause
        [HttpPost]
        [Route("api/resume")]
        public async Task<HttpResponseMessage> PostResume()
        {
            Console.WriteLine("action: resume");
            Stopwatch sw=new Stopwatch();
            Stream req = Request.Body;
            //req.Seek(0, System.IO.SeekOrigin.Begin);
            string json = new StreamReader(req).ReadToEnd();
            JObject o = JObject.Parse(json);
            Guid workflowID;
            if(!Guid.TryParse(o["workflowID"].ToString().Substring(16),out workflowID))
            {
                throw new Exception($"Parse workflowID failed! For {o["workflowID"].ToString().Substring(16)}");
            }
            Console.WriteLine("target: "+workflowID);
            try
            {
                sw.Start();
                await ClientWrapper.Instance.ResumeWorkflow(workflowID);
                sw.Stop();
                Console.WriteLine("Resumed! Time Taken: "+sw.Elapsed);
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
            return new HttpResponseMessage();
        }
    }
}
