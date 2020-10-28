﻿using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Smartstore
{
    public static class NumericExtensions
    {
        #region int

        public static (int lower, int upper) GetRange(this int id, int size = 500)
        {
            // Max [size] values per cache item
            var lower = (int)Math.Floor((decimal)id / size) * size;
            var upper = lower + (size - 1);

            return (lower, upper);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int? ZeroToNull(this int? value)
        {
            return value <= 0 ? null : value;
        }

        #endregion

        #region decimal

        /// <summary>
        /// Calculates the tax (percentage) from a gross and a net value.
        /// </summary>
        /// <param name="inclTax">Gross value</param>
        /// <param name="exclTax">Net value</param>
        /// <param name="decimals">Rounding decimal number</param>
        /// <returns>Tax percentage</returns>
        public static decimal ToTaxPercentage(this decimal inclTax, decimal exclTax, int? decimals = null)
        {
            if (exclTax == decimal.Zero)
            {
                return decimal.Zero;
            }

            var result = ((inclTax / exclTax) - 1.0M) * 100.0M;

            return (decimals.HasValue ? Math.Round(result, decimals.Value) : result);
        }

        /// <summary>
        /// Converts to smallest currency uint, e.g. cents
        /// </summary>
        /// <param name="midpoint">Handling of the midway between two numbers. "ToEven" round down, "AwayFromZero" round up.</param>
        /// <returns>Smallest currency unit</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToSmallestCurrencyUnit(this decimal value, MidpointRounding midpoint = MidpointRounding.AwayFromZero)
        {
            return Convert.ToInt32(Math.Round(value * 100, 0, midpoint));
        }

        /// <summary>
        /// Round decimal to the nearest multiple of denomination
        /// </summary>
        /// <param name="value">Value to round</param>
        /// <param name="denomination">Denomination</param>
        /// <param name="midpoint">Handling of the midway between two numbers. "ToEven" round down, "AwayFromZero" round up.</param>
        /// <returns>Rounded value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal RoundToNearest(this decimal value, decimal denomination, MidpointRounding midpoint = MidpointRounding.AwayFromZero)
        {
            if (denomination == decimal.Zero)
            {
                return value;
            }

            return Math.Round(value / denomination, midpoint) * denomination;
        }

        /// <summary>
        /// Round decimal up or down to the nearest multiple of denomination
        /// </summary>
        /// <param name="value">Value to round</param>
        /// <param name="denomination">Denomination</param>
        /// <param name="roundUp"><c>true</c> round to, <c>false</c> round down</param>
        /// <returns>Rounded value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal RoundToNearest(this decimal value, decimal denomination, bool roundUp)
        {
            if (denomination == decimal.Zero)
            {
                return value;
            }

            var roundedValueBase = roundUp
                ? Math.Ceiling(value / denomination)
                : Math.Floor(value / denomination);

            return Math.Round(roundedValueBase) * denomination;
        }

        /// <summary>
        /// Rounds and formats a decimal culture invariant
        /// </summary>
        /// <param name="value">Value to round</param>
        /// <param name="decimals">Rounding decimal number</param>
        /// <returns>Rounded and formated value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FormatInvariant(this decimal value, int decimals = 2)
        {
            return Math.Round(value, decimals).ToString("0.00", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
