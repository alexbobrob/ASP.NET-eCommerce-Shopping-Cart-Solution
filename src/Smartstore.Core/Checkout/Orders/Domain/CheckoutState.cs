﻿using System.Collections.Generic;

namespace Smartstore.Core.Checkout.Orders
{
    public partial class CheckoutState
    {
        public static string CheckoutStateSessionKey => "SmCheckoutState";

        /// <summary>
        /// The payment summary as displayed on the checkout confirmation page
        /// </summary>
        public string PaymentSummary { get; set; }

        /// <summary>
        /// Indicates whether the payment method selection page was skipped
        /// </summary>
        public bool IsPaymentSelectionSkipped { get; set; }

        /// <summary>
        /// Use this dictionary for any custom data required along checkout flow
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new();

        /// <summary>
        /// The payment data entered on payment method selection page
        /// </summary>
        public Dictionary<string, object> PaymentData { get; set; } = new();
    }
}