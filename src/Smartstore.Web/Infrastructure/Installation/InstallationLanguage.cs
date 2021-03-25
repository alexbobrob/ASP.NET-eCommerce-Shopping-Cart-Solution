﻿using System.Collections.Generic;

namespace Smartstore.Web.Infrastructure.Installation
{
    /// <summary>
    /// Language class for installation process.
    /// </summary>
    public partial class InstallationLanguage
    {
        /// <summary>
        /// Language name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Language code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// A value indicating whether the language is the default language.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// A value indicating whether the language is written from right to left.
        /// </summary>
        public bool IsRightToLeft { get; set; }

        /// <summary>
        /// List of all language resources that will be installed during the setup.
        /// </summary>
        public List<InstallationLocaleResource> Resources { get; protected set; } = new();
    }

    public partial class InstallationLocaleResource
    {
        /// <summary>
        /// Local language resource name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Local language resource value.
        /// </summary>
        public string Value { get; set; }
    }
}