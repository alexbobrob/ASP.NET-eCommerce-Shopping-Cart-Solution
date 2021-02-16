﻿using System.Threading.Tasks;

namespace Smartstore.Core.Rules.Rendering
{
    /// <summary>
    /// Interface to provide select list options (remote only) for rules. <seealso cref="RemoteRuleValueSelectList"/>.
    /// </summary>
    public partial interface IRuleOptionsProvider
    {
        // TODO: (mg) (core) An ordinal is missing to sort all registered providers later in controller.
        
        /// <summary>
        /// Indicates whether this provider can provide select list options for a rule expression.
        /// </summary>
        /// <param name="dataSource">Name of the data source.</param>
        /// <returns><c>true</c> can provide options otherwise <c>false</c>.</returns>
        bool Matches(string dataSource);

        /// <summary>
        /// Gets options for a rule.
        /// </summary>
        /// <param name="reason">The reason for the request.</param>
        /// <param name="descriptor">Rule descriptor.</param>
        /// <param name="value">Expression value.</param>
        /// <param name="pageIndex">Page index if provided options are paged.</param>
        /// <param name="pageSize">Page size if provided options are paged.</param>
        /// <param name="searchTerm">Optional search term entered by user in select control.</param>
        /// <returns>Rule options result.</returns>
        Task<RuleOptionsResult> GetOptionsAsync(
            RuleOptionsRequestReason reason,
            RuleDescriptor descriptor,
            string value,
            int pageIndex,
            int pageSize,
            string searchTerm);
    }
}
