﻿using DogusTeknoloji.SmartKPIMiner.Core;
using DogusTeknoloji.SmartKPIMiner.Data.DataAccessObjects;
using DogusTeknoloji.SmartKPIMiner.Helpers;
using DogusTeknoloji.SmartKPIMiner.Model.Database;
using DogusTeknoloji.SmartKPIMiner.Model.ElasticSearch;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace DogusTeknoloji.SmartKPIMiner.Agent
{
    public class SmartKPIMinerAgent : ServiceBase
    {
        protected Timer _mainServiceTimer;
        public SmartKPIMinerAgent()
        {

        }
        public async Task KPIProcessAsync()
        {
            ServiceManager.Initialize();
            KPIService kpiService = ServiceManager._kpiService;
            IList<SearchIndex> searchIndexList = await kpiService.GetSearchIndicesAsync();

            foreach (SearchIndex index in searchIndexList)
            {
                DateTime searchRange = await kpiService.GetSearchRangeAsync(index.IndexId);
                TimeSpan diff = DateTime.Now - searchRange;

                int totalMins = (int)diff.TotalMinutes;

                if (totalMins < CommonFunctions.UnifyingConstant)
                {
                    Console.WriteLine($"{index.UrlAddress}->{index.IndexName} skipped.");
                    continue;// if count is 0 skip this index.
                }

                int count = totalMins / CommonFunctions.UnifyingConstant;

                DateTime newSearchRange = searchRange.AddMinutes(-CommonFunctions.UnifyingConstant);
                for (int i = 0; i < count; i++)
                {
                    newSearchRange = newSearchRange.AddMinutes(CommonFunctions.UnifyingConstant);

                    string jsonbody = ElasticSearchRESTAdapter.GetRequestBody(searchRange);
                    Root responseRoot = await ElasticSearchRESTAdapter.GetResponseFromElasticUrlAsync(index.UrlAddress, index.IndexName, jsonbody);
                    List<AggregationItem> aggregationItems = responseRoot.Aggregation?.GetAsAggregationItems();
                    if (aggregationItems != null)
                    {
                        var task = kpiService.InsertKPIsAsync(aggregationItems, searchIndexId: index.IndexId, logDate: newSearchRange);
                    }
                    Console.WriteLine($"{index.UrlAddress}->{index.IndexName} [{i + 1}/{count}] added.");
                }

                await Task.WhenAll();
            }
            Console.WriteLine("All Done");
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            _mainServiceTimer = new Timer(callback: async state => await KPIProcessAsync(), state: null, dueTime: 0, period: (int)TimeSpan.FromMinutes(15).TotalMilliseconds);
        }
        protected override void OnStop()
        {
            base.OnStop();
            _mainServiceTimer?.Change(dueTime: Timeout.Infinite, period: 0);
            _mainServiceTimer.DisposeAsync();
        }
    }
}