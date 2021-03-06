using System.Threading.Tasks;

namespace Idecom.Host
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Interfaces;
    using Topshelf;
    using Utility;

    public class WantToStartThreadManager
    {
        readonly IContainerAdapter _containerAdapter;
        List<Type> _types;

        public WantToStartThreadManager(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
            _types = AssemblyScanner.GetScannableAssemblies().SelectMany(x => x.GetTypes()).Implementing<IWantToStartAfterServiceStarts>().ToList();
        }


        List<IWantToStartAfterServiceStarts> _resolvedServicesCache;

        public Task BeforeStart(HostStartedContext hostStartedContext)
        {
            return Task.Factory.StartNew(() =>
            {
                lock (_types)
                {
                    _resolvedServicesCache = _types.Select(type =>
                    {
                        var service = _containerAdapter.Resolve<IWantToStartAfterServiceStarts>(type);
                        if (service == null)
                        {
                            _containerAdapter.Configure(type, Lifecycle.Singleton);
                            service = _containerAdapter.Resolve<IWantToStartAfterServiceStarts>(type);
                        }
                        return service;
                    }).ToList();
                }

                foreach (var service in _resolvedServicesCache)
                {
                    try
                    {
                        service.AfterStart();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error starting IWantToStartAfterServiceStarts service: " + e);
                    }
                }
            });
        }

        public Task BeforeStop(HostStopContext hostStopContext)
        {
            return Task.Factory.StartNew(() =>
            {
                foreach (var service in _resolvedServicesCache)
                {
                    try
                    {
                        service.BeforeStop();
                        _containerAdapter.Release(service);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    _types = null;
                }
            });
        }
    }
}