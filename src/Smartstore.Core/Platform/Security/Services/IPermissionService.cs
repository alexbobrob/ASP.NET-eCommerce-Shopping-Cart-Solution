﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Smartstore.Collections;
using Smartstore.Core.Customers;

namespace Smartstore.Core.Security
{
    /// <summary>
    /// Permission service interface.
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// Checks whether given permission is granted.
        /// </summary>
        /// <param name="permissionSystemName">Permission record system name.</param>
        /// <returns><c>true</c> if granted, otherwise <c>false</c>.</returns>
        bool Authorize(string permissionSystemName);

        /// <summary>
        /// Checks whether given permission is granted.
        /// </summary>
        /// <param name="permissionSystemName">Permission record system name.</param>
        /// <param name="customer">Customer.</param>
        /// <returns><c>true</c> if granted, otherwise <c>false</c>.</returns>
        bool Authorize(string permissionSystemName, Customer customer);

        /// <summary>
        /// Checks whether given permission is granted.
        /// </summary>
        /// <param name="permissionSystemName">Permission record system name.</param>
        /// <param name="customer">Customer. If <c>null</c>, customer will be obtained via <see cref="IWorkContext.CurrentCustomer"/>.</param>
        /// <param name="allowByChildPermission">
        /// A value indicating whether the permission is granted if any child permission is granted.
        /// Example: if a customer has not been granted the permission to view a menu item, it should still be displayed if him has been granted the right to view any child item.
        /// </param>
        /// <returns><c>true</c> if granted, otherwise <c>false</c>.</returns>
        Task<bool> AuthorizeAsync(string permissionSystemName, Customer customer = null, bool allowByChildPermission = false);

        /// <summary>
        /// Authorize permission by alias permission name. Required if granular permission migration has not yet run.
        /// Functional only if the old permission resources still exist in the database.
        /// </summary>
        /// <param name="permissionSystemName">Permission record system name.</param>
        /// <returns><c>true</c> if authorized, otherwise <c>false</c>.</returns>
        Task<bool> AuthorizeByAliasAsync(string permissionSystemName);

        /// <summary>
        /// Gets the permission tree for a customer role from cache.
        /// </summary>
        /// <param name="role">Customer role.</param>
        /// <param name="addDisplayNames">A value indicating whether to add permission display names.</param>
        /// <returns>Permission tree.</returns>
        Task<TreeNode<IPermissionNode>> GetPermissionTreeAsync(CustomerRole role, bool addDisplayNames = false);

        /// <summary>
        /// Builds the permission tree for a customer.
        /// </summary>
        /// <param name="customer">Customer.</param>
        /// <param name="addDisplayNames">A value indicating whether to add permission display names.</param>
        /// <returns>Permission tree.</returns>
        Task<TreeNode<IPermissionNode>> BuildCustomerPermissionTreeAsync(Customer customer, bool addDisplayNames = false);

        /// <summary>
        /// Gets system and display names of all permissions.
        /// </summary>
        /// <returns>System and display names.</returns>
        Task<Dictionary<string, string>> GetAllSystemNamesAsync();

        /// <summary>
        /// Get display name for a permission system name.
        /// </summary>
        /// <param name="permissionSystemName">Permission record system name.</param>
        /// <returns>Display name.</returns>
        Task<string> GetDiplayNameAsync(string permissionSystemName);

        /// <summary>
        /// Get detailed unauthorization message.
        /// </summary>
        /// <param name="permissionSystemName">Permission record system name.</param>
        /// <returns>Detailed unauthorization message</returns>
        Task<string> GetUnauthorizedMessageAsync(string permissionSystemName);

        /// <summary>
        /// Installs permissions. Permissions are automatically installed by <see cref="InstallPermissionsInitializer"/>.
        /// </summary>
        /// <param name="permissionProviders">Providers whose permissions are to be installed.</param>
        /// <param name="removeUnusedPermissions">Whether to remove permissions no longer supported by the providers.</param>
        Task InstallPermissionsAsync(IPermissionProvider[] permissionProviders, bool removeUnusedPermissions = false);
    }
}