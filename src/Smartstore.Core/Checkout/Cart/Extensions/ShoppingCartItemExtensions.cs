﻿using System.Collections.Generic;
using System.Linq;
using Smartstore.Core.Identity;

namespace Smartstore.Core.Checkout.Cart
{
    /// <summary>
    /// Shopping cart extension methods
    /// </summary>
    public static class ShoppingCartItemExtensions
    {
        /// <summary>
        /// Gets customer of shopping cart.
        /// </summary>
        /// <returns>
        /// <see cref="Customer"/> of <see cref="OrganizedShoppingCartItem"/> or <c>null</c> if cart is empty.
        /// </returns>
        public static Customer GetCustomer(this IList<OrganizedShoppingCartItem> cart)
        {
            Guard.NotNull(cart, nameof(cart));

            return cart.Count > 0 ? cart[0].Item.Customer : null;
        }

        /// <summary>
        /// Returns a filtered list of <see cref="ShoppingCartItem"/>s by <see cref="ShoppingCartType"/> and <paramref name="storeId"/>
        /// and sorts by <see cref="ShoppingCartItem.Id"/> descending.
        /// </summary>
        /// <param name="cart">The cart collection the filter gets applied on.</param>
        /// <param name="cartType"><see cref="ShoppingCartType"/> to filter by.</param>
        /// <param name="storeId">Store identifier to filter by.</param>
        /// <returns><see cref="List{T}"/> of <see cref="ShoppingCartItem"/>.</returns>
        public static IList<ShoppingCartItem> FilterByCartType(this ICollection<ShoppingCartItem> cart, ShoppingCartType cartType, int? storeId = null)
        {
            Guard.NotNull(cart, nameof(cart));

            // INFO: ICollection<ShoppingCartItem> indicates that this is a POST-query filter.

            var filteredCartItems = cart.Where(x => x.ShoppingCartTypeId == (int)cartType);

            if (storeId.GetValueOrDefault() > 0)
            {
                filteredCartItems = filteredCartItems.Where(x => x.StoreId == storeId.Value);
            }

            return filteredCartItems.OrderByDescending(x => x.Id).ToList();
        }
    }
}