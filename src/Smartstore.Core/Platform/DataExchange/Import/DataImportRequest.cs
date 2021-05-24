﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Smartstore.Scheduling;

namespace Smartstore.Core.DataExchange.Import
{
    public partial class DataImportRequest
    {
        public DataImportRequest(ImportProfile profile)
        {
            Guard.NotNull(profile, nameof(profile));

            Profile = profile;
            ProgressCallback = OnProgress;
        }

        public ImportProfile Profile { get; private set; }

        public bool HasPermission { get; set; }

        public IList<int> EntitiesToImport { get; set; } = new List<int>();

        public ProgressCallback ProgressCallback { get; init; }

        public IDictionary<string, object> CustomData { get; private set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        Task OnProgress(int value, int max, string msg)
        {
            return Task.CompletedTask;
        }
    }
}
