﻿using System;
using System.Collections.Generic;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Web.Infrastructure.Installation
{
    /// <summary>
    /// Localization service for installation process
    /// </summary>
    public partial interface IInstallationLocalizationService
    {
        string GetResource(string resourceName);

        InstallationLanguage GetCurrentLanguage();

        void SaveCurrentLanguage(string languageCode);

        IList<InstallationLanguage> GetAvailableLanguages();

        IEnumerable<InstallationAppLanguageMetadata> GetAvailableAppLanguages();

        Lazy<InvariantSeedData, InstallationAppLanguageMetadata> GetAppLanguage(string culture);
    }
}
