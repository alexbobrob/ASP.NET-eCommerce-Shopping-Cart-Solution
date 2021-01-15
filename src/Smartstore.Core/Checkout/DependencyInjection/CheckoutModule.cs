﻿using Autofac;
using Smartstore.Core.Checkout.Attributes;
using Smartstore.Core.Checkout.GiftCards;
using Smartstore.Core.Checkout.Tax;

namespace Smartstore.Core.DependencyInjection
{
    public sealed class CheckoutModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<CheckoutAttributeFormatter>().As<ICheckoutAttributeFormatter>().InstancePerLifetimeScope();
            builder.RegisterType<CheckoutAttributeMaterializer>().As<ICheckoutAttributeMaterializer>().InstancePerLifetimeScope();
            builder.RegisterType<GiftCardService>().As<IGiftCardService>().InstancePerLifetimeScope();
            builder.RegisterType<TaxService>().As<ITaxService>().InstancePerLifetimeScope();
        }
    }
}