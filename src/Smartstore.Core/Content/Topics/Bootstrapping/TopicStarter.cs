﻿using System;
using Autofac;
using Smartstore.Core.Content.Seo;
using Smartstore.Engine.Builders;
using Smartstore.Engine;
using Smartstore.Core.Content.Topics;

namespace Smartstore.Core.Bootstrapping
{
    public class TopicStarter : StarterBase
    {
        public override bool Matches(IApplicationContext appContext)
            => appContext.IsInstalled;

        public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext, bool isActiveModule)
        {
            builder.RegisterType<TopicXmlSitemapPublisher>().As<IXmlSitemapPublisher>().InstancePerLifetimeScope();
        }
    }
}