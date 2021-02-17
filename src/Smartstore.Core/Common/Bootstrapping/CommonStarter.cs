﻿using Autofac;
using Smartstore.Core.Common;
using Smartstore.Core.Common.Rules;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Rules.Rendering;
using Smartstore.Core.Web;
using Smartstore.Engine;
using Smartstore.Engine.Builders;

namespace Smartstore.Core.Bootstrapping
{
    public sealed class CommonStarter : StarterBase
    {
        public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext, bool isActiveModule)
        {
            builder.RegisterType<GeoCountryLookup>().As<IGeoCountryLookup>().SingleInstance();
            builder.RegisterType<CurrencyService>().As<ICurrencyService>().InstancePerLifetimeScope();
            builder.RegisterType<DateTimeHelper>().As<IDateTimeHelper>().InstancePerLifetimeScope();
            builder.RegisterType<DeliveryTimeService>().As<IDeliveryTimeService>().InstancePerLifetimeScope();
            builder.RegisterType<MeasureService>().As<IMeasureService>().InstancePerLifetimeScope();
            builder.RegisterType<GenericAttributeService>().As<IGenericAttributeService>().InstancePerLifetimeScope();
            builder.RegisterType<DefaultWebHelper>().As<IWebHelper>().InstancePerLifetimeScope();
            builder.RegisterType<UAParserUserAgent>().As<IUserAgent>().InstancePerLifetimeScope();

            // Rule options provider.
            builder.RegisterType<CommonRuleOptionsProvider>().As<IRuleOptionsProvider>().InstancePerLifetimeScope();
        }
    }
}