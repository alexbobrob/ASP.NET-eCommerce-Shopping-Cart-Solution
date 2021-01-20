﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dasync.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.Caching;
using Smartstore.Collections;
using Smartstore.Core.Customers;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Data;
using Smartstore.Data.Batching;

namespace Smartstore.Core.Security
{
    // TODO: (mg) (core) Implement PermissionService
    public partial class PermissionService : IPermissionService
    {
        // {0} = roleId
        internal const string PERMISSION_TREE_KEY = "permission:tree-{0}";
        internal const string PERMISSION_TREE_PATTERN_KEY = "permission:tree-*";

        private static readonly Dictionary<string, string> _permissionAliases = new()
        {
            { Permissions.System.AccessShop, "PublicStoreAllowNavigation" },
            { Permissions.System.AccessBackend, "AccessAdminPanel" }
        };

        private static readonly Dictionary<string, string> _displayNameResourceKeys = new()
        {
            { "read", "Common.Read" },
            { "update", "Common.Edit" },
            { "create", "Common.Create" },
            { "delete", "Common.Delete" },
            { "catalog", "Admin.Catalog" },
            { "product", "Admin.Catalog.Products" },
            { "category", "Admin.Catalog.Categories" },
            { "manufacturer", "Admin.Catalog.Manufacturers" },
            { "variant", "Admin.Catalog.Attributes.ProductAttributes" },
            { "attribute", "Admin.Catalog.Attributes.SpecificationAttributes" },
            { "customer", "Admin.Customers" },
            { "impersonate", "Admin.Customers.Customers.Impersonate" },
            { "role", "Admin.Customers.CustomerRoles" },
            { "order", "Admin.Orders" },
            { "giftcard", "Admin.GiftCards" },
            { "notify", "Common.Notify" },
            { "returnrequest", "Admin.ReturnRequests" },
            { "accept", "Admin.ReturnRequests.Accept" },
            { "promotion", "Admin.Catalog.Products.Promotion" },
            { "affiliate", "Admin.Affiliates" },
            { "campaign", "Admin.Promotions.Campaigns" },
            { "discount", "Admin.Promotions.Discounts" },
            { "newsletter", "Admin.Promotions.NewsLetterSubscriptions" },
            { "cms", "Admin.ContentManagement" },
            { "poll", "Admin.ContentManagement.Polls" },
            { "news", "Admin.ContentManagement.News" },
            { "blog", "Admin.ContentManagement.Blog" },
            { "widget", "Admin.ContentManagement.Widgets" },
            { "topic", "Admin.ContentManagement.Topics" },
            { "menu", "Admin.ContentManagement.Menus" },
            { "forum", "Admin.ContentManagement.Forums" },
            { "messagetemplate", "Admin.ContentManagement.MessageTemplates" },
            { "configuration", "Admin.Configuration" },
            { "country", "Admin.Configuration.Countries" },
            { "language", "Admin.Configuration.Languages" },
            { "setting", "Admin.Configuration.Settings" },
            { "paymentmethod", "Admin.Configuration.Payment.Methods" },
            { "activate", "Admin.Common.Activate" },
            { "authentication", "Admin.Configuration.ExternalAuthenticationMethods" },
            { "currency", "Admin.Configuration.Currencies" },
            { "deliverytime", "Admin.Configuration.DeliveryTimes" },
            { "theme", "Admin.Configuration.Themes" },
            { "measure", "Admin.Configuration.Measures.Dimensions" },
            { "activitylog", "Admin.Configuration.ActivityLog.ActivityLogType" },
            { "acl", "Admin.Configuration.ACL" },
            { "emailaccount", "Admin.Configuration.EmailAccounts" },
            { "store", "Admin.Common.Stores" },
            { "shipping", "Admin.Configuration.Shipping.Methods" },
            { "tax", "Admin.Configuration.Tax.Providers" },
            { "plugin", "Admin.Configuration.Plugins" },
            { "upload", "Common.Upload" },
            { "install", "Admin.Configuration.Plugins.Fields.Install" },
            { "license", "Admin.Common.License" },
            { "export", "Common.Export" },
            { "execute", "Admin.Common.Go" },
            { "import", "Common.Import" },
            { "system", "Admin.System" },
            { "log", "Admin.System.Log" },
            { "message", "Admin.System.QueuedEmails" },
            { "send", "Common.Send" },
            { "maintenance", "Admin.System.Maintenance" },
            { "scheduletask", "Admin.System.ScheduleTasks" },
            { "urlrecord", "Admin.System.SeNames" },
            { "cart", "ShoppingCart" },
            { "checkoutattribute", "Admin.Catalog.Attributes.CheckoutAttributes" },
            { "media", "Admin.Plugins.KnownGroup.Media" },
            { "download", "Common.Downloads" },
            { "productreview", "Admin.Catalog.ProductReviews" },
            { "approve", "Common.Approve" },
            { "rule", "Common.Rules" },
        };

        private readonly SmartDbContext _db;
        private readonly IWorkContext _workContext;
        private readonly ILocalizationService _localizationService;
        private readonly ICacheManager _cache;

        public PermissionService(
            SmartDbContext db,
            IWorkContext workContext,
            ILocalizationService localizationService,
            ICacheManager cache)
        {
            _db = db;
            _workContext = workContext;
            _localizationService = localizationService;
            _cache = cache;
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public bool Authorize(string permissionSystemName)
        {
            return Authorize(permissionSystemName, _workContext.CurrentCustomer);
        }

        public bool Authorize(string permissionSystemName, Customer customer)
        {
            // TODO: (mg) (core) Really absolutely sure to do the whole authorization thing again in sync (see GetPermissionTreeAsync(CustomerRole))?
            // TODO: (mg) (core) (info) Yes, because low-level stuff should also have a sync counterpart and permission tree comes from cache most of the time.
            return true;
        }

        public async Task<bool> AuthorizeAsync(string permissionSystemName, Customer customer = null, bool allowByChildPermission = false)
        {
            if (string.IsNullOrEmpty(permissionSystemName))
            {
                return false;
            }

            customer ??= _workContext.CurrentCustomer;

            var cacheKey = $"permission.{customer.Id}.{allowByChildPermission}.{permissionSystemName}";

            var authorized = await _cache.GetAsync(cacheKey, async o =>
            {
                o.ExpiresIn(TimeSpan.FromSeconds(30));

                var roles = customer.CustomerRoleMappings
                    .Where(x => x.CustomerRole?.Active ?? false)
                    .Select(x => x.CustomerRole)
                    .ToArray();

                foreach (var role in roles)
                {
                    var tree = await GetPermissionTreeAsync(role);
                    var node = tree.SelectNodeById(permissionSystemName);
                    if (node == null)
                    {
                        continue;
                    }

                    if (allowByChildPermission && FindAllowByChild(node))
                    {
                        return true;
                    }

                    while (node != null && !node.Value.Allow.HasValue)
                    {
                        node = node.Parent;
                    }

                    if (node?.Value?.Allow ?? false)
                    {
                        return true;
                    }
                }

                return false;
            });

            return authorized;

            static bool FindAllowByChild(TreeNode<IPermissionNode> n)
            {
                if (n?.Value?.Allow ?? false)
                {
                    return true;
                }

                if (n.HasChildren)
                {
                    foreach (var child in n.Children)
                    {
                        if (FindAllowByChild(child))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        public async Task<bool> AuthorizeByAliasAsync(string permissionSystemName)
        {
            if (string.IsNullOrEmpty(permissionSystemName) || !_permissionAliases.TryGetValue(permissionSystemName, out var alias))
            {
                return false;
            }

            var aliasPermission = await _db.PermissionRecords
                .ApplySystemNameFilter(permissionSystemName)
                .FirstOrDefaultAsync();

            if (aliasPermission == null)
            {
                return false;
            }

            // SQL required because the old mapping was only accessible via navigation property but it no longer exists.
            if (await _db.DataProvider.HasTableAsync("PermissionRecord_Role_Mapping"))
            {
                var aliasCutomerRoleIds = await _db.Database
                    .ExecuteQueryRawAsync<int>("select [CustomerRole_Id] from [dbo].[PermissionRecord_Role_Mapping] where [PermissionRecord_Id] = " + aliasPermission.Id)
                    .ToListAsync();

                if (aliasCutomerRoleIds.Any())
                {
                    var roles = _workContext.CurrentCustomer.CustomerRoleMappings
                        .Select(x => x.CustomerRole)
                        .Where(x => x.Active);

                    foreach (var role in roles)
                    {
                        if (aliasCutomerRoleIds.Contains(role.Id))
                        {
                            return true;
                        }
                    }
                }
            }

            return true;
        }

        public async Task<TreeNode<IPermissionNode>> GetPermissionTreeAsync(CustomerRole role, bool addDisplayNames = false)
        {
            Guard.NotNull(role, nameof(role));

            var result = await _cache.GetAsync(PERMISSION_TREE_KEY.FormatInvariant(role.Id), async o =>
            {
                var root = new TreeNode<IPermissionNode>(new PermissionNode());

                var allPermissions = await _db.PermissionRecords
                    .AsNoTracking()
                    .Include(x => x.PermissionRoleMappings)
                    .ToListAsync();

                await AddPermissions(root, GetChildren(null, allPermissions), allPermissions, null, role);

                return root;
            });

            if (addDisplayNames)
            {
                // Adds the localized display name to permission nodes as thread metadata. Only required for backend.
                var resourcesLookup = await GetDisplayNameLookup(_workContext.WorkingLanguage.Id);
                AddDisplayNames(result, resourcesLookup);
            }

            return result;
        }

        public async Task<TreeNode<IPermissionNode>> BuildCustomerPermissionTreeAsync(Customer customer, bool addDisplayNames = false)
        {
            Guard.NotNull(customer, nameof(customer));

            var root = new TreeNode<IPermissionNode>(new PermissionNode());
            var allPermissions = await _db.PermissionRecords
                .AsNoTracking()
                .ToListAsync();

            await AddPermissions(root, GetChildren(null, allPermissions), allPermissions, customer, null);

            if (addDisplayNames)
            {
                var resourcesLookup = await GetDisplayNameLookup(_workContext.WorkingLanguage.Id);
                AddDisplayNames(root, resourcesLookup);
            }

            return root;
        }

        public async Task<Dictionary<string, string>> GetAllSystemNamesAsync()
        {
            var result = new Dictionary<string, string>();
            var resourcesLookup = await GetDisplayNameLookup(_workContext.WorkingLanguage.Id);

            var systemNames = await _db.PermissionRecords
                .AsQueryable()
                .Select(x => x.SystemName)
                .ToListAsync();

            foreach (var systemName in systemNames)
            {
                var safeSytemName = systemName.EmptyNull().ToLower();
                var tokens = safeSytemName.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

                result[safeSytemName] = GetDisplayName(tokens, resourcesLookup);
            }

            return result;
        }

        public async Task<string> GetDiplayNameAsync(string permissionSystemName)
        {
            var tokens = permissionSystemName.EmptyNull().ToLower().Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Any())
            {
                var resourcesLookup = await GetDisplayNameLookup(_workContext.WorkingLanguage.Id);

                return GetDisplayName(tokens, resourcesLookup);
            }

            return string.Empty;
        }

        public async Task<string> GetUnauthorizedMessageAsync(string permissionSystemName)
        {
            var displayName = await GetDiplayNameAsync(permissionSystemName);
            var message = await _localizationService.GetResourceAsync("Admin.AccessDenied.DetailedDescription");

            return message.FormatInvariant(displayName.NaIfEmpty(), permissionSystemName.NaIfEmpty());
        }

        public async Task InstallPermissionsAsync(IPermissionProvider[] permissionProviders, bool removeUnusedPermissions = false)
        {
            if (!(permissionProviders?.Any() ?? false))
            {
                return;
            }

            var allPermissionNames = await _db.PermissionRecords
                .AsQueryable()
                .Select(x => x.SystemName)
                .ToListAsync();

            Dictionary<string, CustomerRole> existingRoles = null;
            var existing = new HashSet<string>(allPermissionNames, StringComparer.InvariantCultureIgnoreCase);
            var added = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var providerPermissions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var log = existing.Any();
            var clearCache = false;

            if (existing.Any())
            {
                var permissionsMigrated = existing.Contains(Permissions.System.AccessShop) && !existing.Contains("PublicStoreAllowNavigation");
                if (!permissionsMigrated)
                {
                    // Migrations must have been completed before permissions can be added or deleted.
                    return;
                }
            }

            try
            {
                using (var scope = new DbContextScope(_db, hooksEnabled: false))
                {
                    // Add new permissions.
                    foreach (var provider in permissionProviders)
                    {
                        try
                        {
                            var systemNames = provider.GetPermissions().Select(x => x.SystemName);
                            var missingSystemNames = systemNames.Except(existing);

                            if (removeUnusedPermissions)
                            {
                                providerPermissions.AddRange(systemNames);
                            }

                            if (missingSystemNames.Any())
                            {
                                var defaultPermissions = provider.GetDefaultPermissions();
                                foreach (var systemName in missingSystemNames)
                                {
                                    var roleNames = defaultPermissions
                                        .Where(x => x.PermissionRecords.Any(y => y.SystemName == systemName))
                                        .Select(x => x.CustomerRoleSystemName);

                                    var newPermission = new PermissionRecord { SystemName = systemName };

                                    foreach (var roleName in new HashSet<string>(roleNames, StringComparer.InvariantCultureIgnoreCase))
                                    {
                                        if (existingRoles == null)
                                        {
                                            existingRoles = new Dictionary<string, CustomerRole>();

                                            var rolesPager = _db.CustomerRoles
                                                .AsNoTracking()
                                                .Where(x => !string.IsNullOrEmpty(x.SystemName))
                                                .ToFastPager(500);

                                            while ((await rolesPager.ReadNextPageAsync<CustomerRole>()).Out(out var roles))
                                            {
                                                roles.Each(x => existingRoles[x.SystemName] = x);
                                            }
                                        }

                                        if (!existingRoles.TryGetValue(roleName, out var role))
                                        {
                                            role = new CustomerRole
                                            {
                                                Active = true,
                                                Name = roleName,
                                                SystemName = roleName
                                            };

                                            await _db.CustomerRoles.AddAsync(role);

                                            await scope.CommitAsync();
                                            existingRoles[roleName] = role;
                                        }

                                        newPermission.PermissionRoleMappings.Add(new PermissionRoleMapping
                                        {
                                            Allow = true,
                                            CustomerRoleId = role.Id
                                        });
                                    }

                                    // TODO: (mg) (core) DbSet.AddAsync() does not save anything. It just begins tracking!
                                    await _db.PermissionRecords.AddAsync(newPermission);

                                    clearCache = true;
                                    added.Add(newPermission.SystemName);
                                    existing.Add(newPermission.SystemName);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex);
                        }
                    }

                    await scope.CommitAsync();

                    if (log && added.Any())
                    {
                        var message = await _localizationService.GetResourceAsync("Admin.Permissions.AddedPermissions");
                        Logger.Info(message.FormatInvariant(string.Join(", ", added)));
                    }

                    // Remove permissions no longer supported by providers.
                    if (removeUnusedPermissions)
                    {
                        var toDelete = existing.Except(providerPermissions).ToList();
                        if (toDelete.Any())
                        {
                            clearCache = true;

                            foreach (var chunk in toDelete.Slice(500))
                            {
                                await _db.PermissionRecords
                                    .AsQueryable()
                                    .Where(x => chunk.Contains(x.SystemName))
                                    .BatchDeleteAsync();
                            }

                            if (log)
                            {
                                var message = await _localizationService.GetResourceAsync("Admin.Permissions.RemovedPermissions");
                                Logger.Info(message.FormatInvariant(string.Join(", ", toDelete)));
                            }
                        }
                    }
                }
            }
            finally
            {
                if (clearCache)
                {
                    await _cache.RemoveByPatternAsync(PERMISSION_TREE_PATTERN_KEY);
                }
            }
        }

        #region Utilities

        private static IEnumerable<PermissionRecord> GetChildren(PermissionRecord permission, IEnumerable<PermissionRecord> allPermissions)
        {
            if (permission == null)
            {
                // Get root permissions.
                return allPermissions.Where(x => !x.SystemName.Contains('.'));
            }
            else
            {
                // Get children.
                var tmpPath = permission.SystemName.EnsureEndsWith(".");

                return allPermissions.Where(x => x.SystemName.StartsWith(tmpPath) && x.SystemName.IndexOf('.', tmpPath.Length) == -1);
            }
        }

        private async Task AddPermissions(
            TreeNode<IPermissionNode> parent,
            IEnumerable<PermissionRecord> toAdd,
            List<PermissionRecord> allPermissions,
            Customer customer,
            CustomerRole role)
        {
            foreach (var entity in toAdd)
            {
                // null = inherit
                bool? allow = null;

                if (role != null)
                {
                    var mapping = entity.PermissionRoleMappings.FirstOrDefault(x => x.CustomerRoleId == role.Id);
                    allow = mapping?.Allow ?? null;
                }
                else
                {
                    allow = await AuthorizeAsync(entity.SystemName, customer);
                }                

                var newNode = parent.Append(new PermissionNode
                {
                    PermissionRecordId = entity.Id,
                    Allow = allow,
                    SystemName = entity.SystemName
                }, entity.SystemName);

                await AddPermissions(newNode, GetChildren(entity, allPermissions), allPermissions, customer, role);
            }
        }

        private static void AddDisplayNames(TreeNode<IPermissionNode> node, Dictionary<string, string> resourcesLookup)
        {
            var tokens = node.Value.SystemName.EmptyNull().ToLower().Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var token = tokens.LastOrDefault();
            var displayName = GetDisplayName(token, resourcesLookup);

            node.SetThreadMetadata("DisplayName", displayName ?? token ?? node.Value.SystemName);

            if (node.HasChildren)
            {
                foreach (var children in node.Children)
                {
                    AddDisplayNames(children, resourcesLookup);
                }
            }
        }

        private static string GetDisplayName(string[] tokens, Dictionary<string, string> resourcesLookup)
        {
            var displayName = string.Empty;

            if (tokens?.Any() ?? false)
            {
                foreach (var token in tokens)
                {
                    if (displayName.Length > 0)
                    {
                        displayName += " » ";
                    }

                    displayName += GetDisplayName(token, resourcesLookup) ?? token ?? string.Empty;
                }
            }

            return displayName;
        }

        private static string GetDisplayName(string token, Dictionary<string, string> resourcesLookup)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            // Try known token of default permissions.
            if (_displayNameResourceKeys.TryGetValue(token, out string key) && resourcesLookup.TryGetValue(key, out string name))
            {
                return name;
            }

            // Unknown token. Try to find resource by name convention.
            key = "Permissions.DisplayName." + token.Replace("-", "");
            if (resourcesLookup.TryGetValue(key, out name))
            {
                return name;
            }

            // Try resource provided by plugin.
            key = "Plugins." + key;
            if (resourcesLookup.TryGetValue(key, out name))
            {
                return name;
            }

            return null;
        }

        private async Task<Dictionary<string, string>> GetDisplayNameLookup(int languageId)
        {
            var displayNames = await _cache.GetAsync("permission:displayname-" + languageId, async o =>
            {
                o.ExpiresIn(TimeSpan.FromDays(1));

                var allKeys = _displayNameResourceKeys.Select(x => x.Value);

                var resources = await _db.LocaleStringResources
                    .AsNoTracking()
                    .Where(x => x.LanguageId == languageId && 
                        (x.ResourceName.StartsWith("Permissions.DisplayName.") || x.ResourceName.StartsWith("Plugins.Permissions.DisplayName.") || allKeys.Contains(x.ResourceName)) &&
                        !string.IsNullOrEmpty(x.ResourceValue))
                    .ToListAsync();

                return resources.ToDictionarySafe(x => x.ResourceName, x => x.ResourceValue, StringComparer.OrdinalIgnoreCase);
            });

            return displayNames;
        }

        #endregion
    }
}
