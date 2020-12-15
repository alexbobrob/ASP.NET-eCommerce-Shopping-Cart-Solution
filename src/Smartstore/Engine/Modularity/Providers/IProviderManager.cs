﻿using System.Collections.Generic;

namespace Smartstore.Engine.Modularity
{
    public interface IProviderManager
    {
        Provider<TProvider> GetProvider<TProvider>(string systemName, int storeId = 0) where TProvider : IProvider;

        Provider<IProvider> GetProvider(string systemName, int storeId = 0);

        IEnumerable<Provider<TProvider>> GetAllProviders<TProvider>(int storeId = 0) where TProvider : IProvider;

        IEnumerable<Provider<IProvider>> GetAllProviders(int storeId = 0);
    }
}