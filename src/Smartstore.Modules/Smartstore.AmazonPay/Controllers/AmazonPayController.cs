﻿using System.IO;
using System.Linq;
using Amazon.Pay.API.WebStore.Buyer;
using Amazon.Pay.API.WebStore.CheckoutSession;
using Amazon.Pay.API.WebStore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Smartstore.AmazonPay.Services;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Checkout.Attributes;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Common;
using Smartstore.Core.Data;
using Smartstore.Utilities.Html;
using Smartstore.Web.Controllers;

namespace Smartstore.AmazonPay.Controllers
{
    public class AmazonPayController : PublicController
    {
        private readonly SmartDbContext _db;
        private readonly IAmazonPayService _amazonPayService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IShoppingCartValidator _shoppingCartValidator;
        private readonly ICheckoutAttributeMaterializer _checkoutAttributeMaterializer;
        private readonly ICheckoutStateAccessor _checkoutStateAccessor;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderCalculationService _orderCalculationService;
        private readonly IPaymentService _paymentService;
        private readonly AmazonPaySettings _settings;
        private readonly OrderSettings _orderSettings;

        public AmazonPayController(
            SmartDbContext db,
            IAmazonPayService amazonPayService,
            IShoppingCartService shoppingCartService,
            IShoppingCartValidator shoppingCartValidator,
            ICheckoutAttributeMaterializer checkoutAttributeMaterializer,
            ICheckoutStateAccessor checkoutStateAccessor,
            IOrderProcessingService orderProcessingService,
            IOrderCalculationService orderCalculationService,
            IPaymentService paymentService,
            AmazonPaySettings amazonPaySettings,
            OrderSettings orderSettings)
        {
            _db = db;
            _amazonPayService = amazonPayService;
            _shoppingCartService = shoppingCartService;
            _shoppingCartValidator = shoppingCartValidator;
            _checkoutAttributeMaterializer = checkoutAttributeMaterializer;
            _checkoutStateAccessor = checkoutStateAccessor;
            _orderProcessingService = orderProcessingService;
            _orderCalculationService = orderCalculationService;
            _paymentService = paymentService;
            _settings = amazonPaySettings;
            _orderSettings = orderSettings;
        }

        /// <summary>
        /// AJAX. Creates the AmazonPay checkout session object after clicking the AmazonPay button.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(ProductVariantQuery query, bool? useRewardPoints)
        {
            var signature = string.Empty;
            var payload = string.Empty;
            var message = string.Empty;
            var messageType = string.Empty;

            try
            {
                var store = Services.StoreContext.CurrentStore;
                var customer = Services.WorkContext.CurrentCustomer;
                var currentScheme = Services.WebHelper.IsCurrentConnectionSecured() ? "https" : "http";
                var cart = await _shoppingCartService.GetCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
                var warnings = new List<string>();

                // Save data entered on cart page.
                var isCartValid = await _shoppingCartService.SaveCartDataAsync(cart, warnings, query, useRewardPoints);
                if (isCartValid)
                {
                    var client = HttpContext.GetAmazonPayApiClient(store.Id);
                    var checkoutReviewUrl = Url.Action(nameof(CheckoutReview), "AmazonPay", null, currentScheme);
                    var request = new CreateCheckoutSessionRequest(checkoutReviewUrl, _settings.ClientId)
                    {
                        PlatformId = AmazonPayProvider.PlatformId
                    };

                    // TODO later: config for specialRestrictions 'RestrictPOBoxes', 'RestrictPackstations'.
                    if (cart.HasItems && cart.IsShippingRequired())
                    {
                        var allowedCountryCodes = await _db.Countries
                            .ApplyStandardFilter(false, store.Id)
                            .Where(x => x.AllowsBilling || x.AllowsShipping)
                            .Select(x => x.TwoLetterIsoCode)
                            .ToListAsync();

                        if (allowedCountryCodes.Any())
                        {
                            request.DeliverySpecifications.AddressRestrictions.Type = RestrictionType.Allowed;
                            allowedCountryCodes
                                .Distinct()
                                .Each(countryCode => request.DeliverySpecifications.AddressRestrictions.AddCountryRestriction(countryCode));
                        }
                    }

                    payload = request.ToJsonNoType();
                    signature = client.GenerateButtonSignature(payload);
                }
                else
                {
                    message = string.Join(Environment.NewLine, warnings);
                    messageType = "warning";
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                messageType = "error";
            }

            return Json(new
            {
                success = signature.HasValue(),
                signature,
                payload,
                message,
                messageType
            });
        }

        /// <summary>
        /// The buyer is redirected to this action method after they complete checkout on the AmazonPay hosted page.
        /// </summary>
        [Route("amazonpay/checkoutreview")]
        public async Task<IActionResult> CheckoutReview(string amazonCheckoutSessionId)
        {
            try
            {
                var result = await ProcessCheckoutReview(amazonCheckoutSessionId);

                if (result.Success)
                {
                    var actionName = result.IsShippingMethodMissing
                        ? nameof(CheckoutController.ShippingMethod)
                        : nameof(CheckoutController.Confirm);

                    return RedirectToAction(actionName, "Checkout");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                NotifyError(ex);
            }

            return RedirectToRoute("ShoppingCart");
        }

        private async Task<CheckoutReviewResult> ProcessCheckoutReview(string checkoutSessionId)
        {
            var result = new CheckoutReviewResult();

            if (checkoutSessionId.IsEmpty())
            {
                NotifyWarning(T("Plugins.Payments.AmazonPay.PaymentFailure"));
                return result;
            }

            var store = Services.StoreContext.CurrentStore;
            var cart = await _shoppingCartService.GetCartAsync(storeId: store.Id);
            var isShippingRequired = cart.IsShippingRequired();
            var customer = cart.Customer;

            result.IsShippingMethodMissing = isShippingRequired && customer.GenericAttributes.SelectedShippingOption == null;

            if (!cart.HasItems)
            {
                return result;
            }

            if (customer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed)
            {
                NotifyWarning(T("Checkout.AnonymousNotAllowed"));
                return result;
            }

            await _db.LoadCollectionAsync(customer, x => x.Addresses);

            // Create addresses from AmazonPay checkout session.
            var client = HttpContext.GetAmazonPayApiClient(store.Id);
            var session = client.GetCheckoutSession(checkoutSessionId);

            if (session.BillingAddress == null)
            {
                NotifyError(T("Plugins.Payments.AmazonPay.MissingBillingAddress"));
                return result;
            }

            var billTo = await _amazonPayService.CreateAddressAsync(session, customer, true);
            if (!billTo.Success)
            {
                // We have to redirect the buyer back to the shopping cart because we cannot change the address at this stage.
                // We cannot store invalid addresses and assign them to a customer.
                NotifyWarning(T("Plugins.Payments.AmazonPay.BillingToCountryNotAllowed"));
                result.RequiresAddressUpdate = true;
                return result;
            }

            CheckoutAdressResult shipTo = null;

            if (isShippingRequired)
            {
                shipTo = await _amazonPayService.CreateAddressAsync(session, customer, false);
                if (!shipTo.Success)
                {
                    NotifyWarning(T("Plugins.Payments.AmazonPay.ShippingToCountryNotAllowed"));
                    result.RequiresAddressUpdate = true;
                    return result;
                }
            }


            // Update customer.
            var billingAddress = customer.FindAddress(billTo.Address);
            if (billingAddress != null)
            {
                customer.BillingAddress = billingAddress;
            }
            else
            {
                customer.Addresses.Add(billTo.Address);
                customer.BillingAddress = billTo.Address;
            }

            if (shipTo == null)
            {
                customer.ShippingAddress = null;
            }
            else
            {
                var shippingAddress = customer.FindAddress(shipTo.Address);
                if (shippingAddress != null)
                {
                    customer.ShippingAddress = shippingAddress;
                }
                else
                {
                    customer.Addresses.Add(shipTo.Address);
                    customer.ShippingAddress = shipTo.Address;
                }
            }

            customer.GenericAttributes.SelectedPaymentMethod = AmazonPayProvider.SystemName;

            if (_settings.CanSaveEmailAndPhone(customer.Email))
            {
                customer.Email = session.Buyer.Email;
            }

            if (_settings.CanSaveEmailAndPhone(customer.GenericAttributes.Phone))
            {
                customer.GenericAttributes.Phone = billTo.Address.PhoneNumber.NullEmpty() ?? session.Buyer.PhoneNumber;
            }

            await _db.SaveChangesAsync();
            result.Success = true;

            _checkoutStateAccessor.CheckoutState.CustomProperties[AmazonPayCheckoutState.Key] = new AmazonPayCheckoutState 
            {
                CheckoutSessionId = checkoutSessionId
            };

            if (session.PaymentPreferences != null)
            {
                _checkoutStateAccessor.CheckoutState.PaymentSummary = string.Join(", ", session.PaymentPreferences.Select(x => x.PaymentDescriptor));
            }

            if (!HttpContext.Session.TryGetObject<ProcessPaymentRequest>("OrderPaymentInfo", out var paymentRequest) 
                || paymentRequest == null
                || paymentRequest.OrderGuid == Guid.Empty)
            {
                HttpContext.Session.TrySetObject("OrderPaymentInfo", new ProcessPaymentRequest { OrderGuid = Guid.NewGuid() });
            }

            return result;
        }

        /// <summary>
        /// AJAX. Called after buyer clicked buy-now-button but before the order was created.
        /// Validates order placement and updates AmazonPay checkout session to set payment info.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ConfirmOrder(string formData)
        {
            string redirectUrl = null;
            var messages = new List<string>();
            var success = false;

            try
            {
                var store = Services.StoreContext.CurrentStore;
                var customer = Services.WorkContext.CurrentCustomer;

                if (_checkoutStateAccessor.CheckoutState?.CustomProperties?.Get(AmazonPayCheckoutState.Key) is not AmazonPayCheckoutState state)
                {
                    throw new SmartException(T("Plugins.Payments.AmazonPay.MissingCheckoutSessionState"));
                }

                if (!HttpContext.Session.TryGetObject<ProcessPaymentRequest>("OrderPaymentInfo", out var paymentRequest) || paymentRequest == null)
                {
                    paymentRequest = new ProcessPaymentRequest();
                }

                paymentRequest.StoreId = store.Id;
                paymentRequest.CustomerId = customer.Id;
                paymentRequest.PaymentMethodSystemName = AmazonPayProvider.SystemName;

                // We must check here if an order can be placed to avoid creating unauthorized Amazon payment objects.
                var (warnings, cart) = await _orderProcessingService.ValidateOrderPlacementAsync(paymentRequest);

                if (!warnings.Any())
                {
                    if (await _orderProcessingService.IsMinimumOrderPlacementIntervalValidAsync(customer, store))
                    {
                        var currentScheme = Services.WebHelper.IsCurrentConnectionSecured() ? "https" : "http";
                        var cartTotal = (Money?)await _orderCalculationService.GetShoppingCartTotalAsync(cart);
                        var request = new UpdateCheckoutSessionRequest();

                        request.PaymentDetails.ChargeAmount.Amount = cartTotal.Value.Amount;
                        request.PaymentDetails.ChargeAmount.CurrencyCode = _amazonPayService.GetAmazonPayCurrency();
                        request.PaymentDetails.CanHandlePendingAuthorization = _settings.TransactionType == AmazonPayTransactionType.Authorize;
                        request.PaymentDetails.PaymentIntent = _settings.TransactionType == AmazonPayTransactionType.AuthorizeAndCapture
                            ? PaymentIntent.AuthorizeWithCapture
                            : PaymentIntent.Authorize;

                        request.WebCheckoutDetails.CheckoutResultReturnUrl = Url.Action(nameof(ConfirmationResult), "AmazonPay", null, currentScheme);
                        request.MerchantMetadata.MerchantStoreName = store.Name.Truncate(50);

                        if (paymentRequest.OrderGuid != Guid.Empty)
                        {
                            request.MerchantMetadata.MerchantReferenceId = paymentRequest.OrderGuid.ToString();
                        }

                        var client = HttpContext.GetAmazonPayApiClient(store.Id);
                        var response = client.UpdateCheckoutSession(state.CheckoutSessionId, request);

                        if (response.Success)
                        {
                            // INFO: unlike in v1, the constraints can be ignored. They are only returned if mandatory parameters are missing.
                            redirectUrl = response.WebCheckoutDetails.AmazonPayRedirectUrl;

                            if (redirectUrl.HasValue())
                            {
                                success = true;
                                state.IsConfirmed = true;
                                state.FormData = formData.EmptyNull();
                            }
                            else
                            {
                                messages.Add(T("Plugins.Payments.AmazonPay.PaymentFailure"));
                                Logger.LogAmazonPayFailure(request, response);
                            }
                        }
                        else
                        {
                            var message = Logger.LogAmazonPayFailure(request, response);
                            messages.Add(message);
                        }
                    }
                    else
                    {
                        messages.Add(T("Checkout.MinOrderPlacementInterval"));
                    }
                }
                else
                {
                    messages.AddRange(warnings.Select(x => HtmlUtility.ConvertPlainTextToHtml(x)));
                }
            }
            catch (Exception ex)
            {
                messages.Add(ex.Message);
                Logger.Error(ex);
            }

            return Json(new { success, redirectUrl, messages });
        }

        /// <summary>
        /// The buyer is redirected to this action method after checkout is completed on the AmazonPay hosted page.
        /// </summary>
        [Route("amazonpay/confirmationresult")]
        public IActionResult ConfirmationResult()
        {
            try
            {
                if (_checkoutStateAccessor.CheckoutState?.CustomProperties?.Get(AmazonPayCheckoutState.Key) is not AmazonPayCheckoutState state)
                {
                    throw new SmartException(T("Plugins.Payments.AmazonPay.MissingCheckoutSessionState"));
                }

                state.SubmitForm = false;

                // INFO: amazonCheckoutSessionId query parameter is provided here too but it is more secure to use the state object.
                if (state.CheckoutSessionId.IsEmpty())
                {
                    throw new SmartException(T("Plugins.Payments.AmazonPay.MissingCheckoutSessionState"));
                }

                var client = HttpContext.GetAmazonPayApiClient(Services.StoreContext.CurrentStore.Id);
                var response = client.GetCheckoutSession(state.CheckoutSessionId);

                if (response.Success)
                {
                    if (!response.StatusDetails.State.EqualsNoCase("Canceled"))
                    {
                        state.SubmitForm = true;
                        return RedirectToAction(nameof(CheckoutController.Confirm), "Checkout");
                    }
                    else
                    {
                        NotifyError(T("Plugins.Payments.AmazonPay.AuthenticationStatusFailureMessage"));
                    }
                }
                else
                {
                    NotifyError(T("Plugins.Payments.AmazonPay.AuthenticationStatusFailureMessage"));
                    Logger.LogAmazonPayFailure(null, response);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                NotifyError(ex);
            }

            return RedirectToRoute("ShoppingCart");
        }

        [HttpPost]
        [Route("amazonpay/ipnhandler")]
        public async Task<IActionResult> IPNHandler()
        {
            string json = null;

            try
            {
                using (var reader = new StreamReader(Request.Body))
                {
                    json = await reader.ReadToEndAsync();
                }

                if (json.HasValue())
                {
                    dynamic ipnEnvelope = JsonConvert.DeserializeObject(json);
                    var message = JsonConvert.DeserializeObject<IpnMessage>((string)ipnEnvelope.Message);

                    if (message != null)
                    {
                        await ProcessIpn(message, (string)ipnEnvelope.MessageId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, json);
            }

            return Ok();
        }

        private async Task ProcessIpn(IpnMessage message, string messageId)
        {
            string newState = null;
            var orderUpdated = false;
            var authorize = false;
            var paid = false;
            var voidOffline = false;
            var refund = false;
            var refundAmount = decimal.Zero;
            var chargeback = message.ObjectType.EqualsNoCase("CHARGEBACK");

            var chargePermissionId = message.ObjectType.EqualsNoCase("CHARGE_PERMISSION")
                ? message.ObjectId
                : message.ChargePermissionId;

            if (chargePermissionId.IsEmpty())
            {
                Logger.Warn(T("Plugins.Payments.AmazonPay.OrderNotFound", chargePermissionId));
                return;
            }

            var client = HttpContext.GetAmazonPayApiClient(Services.StoreContext.CurrentStore.Id);

            if (message.ObjectType.EqualsNoCase("CHARGE_PERMISSION"))
            {                
                var response = client.GetChargePermission(message.ObjectId);
                if (response.Success)
                {
                    var d = response.StatusDetails;
                    newState = d.State.Grow(d.Reasons?.LastOrDefault()?.ReasonCode, " ");
                    authorize = true;
                    voidOffline = d.State.EqualsNoCase("Closed") || d.State.EqualsNoCase("NonChargeable");
                }
                else
                {
                    Logger.LogAmazonPayFailure(null, response);
                }
            }
            else if (message.ObjectType.EqualsNoCase("CHARGE"))
            {
                var response = client.GetCharge(message.ObjectId);
                if (response.Success)
                {
                    var d = response.StatusDetails;
                    var isDeclined = d.State.EqualsNoCase("Declined");
                    newState = d.State.Grow(d.ReasonCode, " ");

                    // Authorize if not still pending.
                    authorize = !d.State.EqualsNoCase("AuthorizationInitiated");
                    paid = d.State.EqualsNoCase("Captured");

                    // "SoftDeclined... retry attempts may or may not be successful":
                    // We can not distinguish in it in terms of further processing -> void payment.
                    voidOffline = isDeclined || d.State.EqualsNoCase("Canceled");

                    if (isDeclined && d.ReasonCode.EqualsNoCase("ProcessingFailure") && message.ChargePermissionId.HasValue())
                    {
                        var response2 = client.GetChargePermission(message.ChargePermissionId);
                        if (response2.Success)
                        {
                            voidOffline = !response2.StatusDetails.State.EqualsNoCase("Chargeable");
                        }
                        else
                        {
                            Logger.LogAmazonPayFailure(null, response2);
                        }
                    }
                }
                else
                {
                    Logger.LogAmazonPayFailure(null, response);
                }
            }
            else if (message.ObjectType.EqualsNoCase("REFUND"))
            {
                var response = client.GetRefund(message.ObjectId);
                if (response.Success)
                {
                    var d = response.StatusDetails;
                    newState = d.State.Grow(d.ReasonCode, " ");
                    refund = d.State.EqualsNoCase("Refunded");

                    if (refund)
                    {
                        refundAmount = response.RefundAmount.Amount;
                    }
                }
                else
                {
                    Logger.LogAmazonPayFailure(null, response);
                }
            }

            // Perf. Jump out early from further processing.
            // Access the database only when necessary.
            if (!authorize && !paid && !voidOffline && !refund && !chargeback)
            {
                return;
            }

            // Get order.
            var order = await _db.Orders.FirstOrDefaultAsync(x => x.PaymentMethodSystemName == AmazonPayProvider.SystemName && x.AuthorizationTransactionCode == chargePermissionId);
            if (order == null)
            {
                Logger.Warn(T("Plugins.Payments.AmazonPay.OrderNotFound", chargePermissionId));
                return;
            }

            if (!await _paymentService.IsPaymentMethodActiveAsync(AmazonPayProvider.SystemName, null, order.StoreId))
            {
                return;
            }

            // Process order.
            var oldState = order.CaptureTransactionResult.NullEmpty() ?? order.AuthorizationTransactionResult.NullEmpty() ?? "-";

            // INFO: order must be authorized for all other state changes.
            // That is why we call MarkAsAuthorizedAsync, even though the payment is not necessarily considered authorized at AmazonPay.
            if (authorize && order.CanMarkOrderAsAuthorized())
            {
                order.AuthorizationTransactionResult = newState;

                await _orderProcessingService.MarkAsAuthorizedAsync(order);
                orderUpdated = true;
            }

            if (paid && order.CanMarkOrderAsPaid())
            {
                order.CaptureTransactionResult = newState;

                await _orderProcessingService.MarkOrderAsPaidAsync(order);
                orderUpdated = true;
            }

            if (voidOffline && order.CanVoidOffline())
            {
                order.CaptureTransactionResult = newState;

                await _orderProcessingService.VoidOfflineAsync(order);
                orderUpdated = true;
            }

            // Only refund once because order.RefundedAmount could become wrong otherwise.
            if (refund && order.RefundedAmount == decimal.Zero && refundAmount > decimal.Zero)
            {
                decimal receivable = order.OrderTotal - refundAmount;
                if (receivable <= decimal.Zero)
                {
                    if (order.CanRefundOffline())
                    {
                        order.CaptureTransactionResult = newState;

                        await _orderProcessingService.RefundOfflineAsync(order);
                        orderUpdated = true;
                    }
                }
                else
                {
                    if (order.CanPartiallyRefundOffline(refundAmount))
                    {
                        order.CaptureTransactionResult = newState;

                        await _orderProcessingService.PartiallyRefundOfflineAsync(order, refundAmount);
                        orderUpdated = true;
                    }
                }
            }

            // Add order note.
            if ((orderUpdated || chargeback) && _settings.AddOrderNotes)
            {
                var faviconUrl = Services.WebHelper.GetStoreLocation() + "Modules/Smartstore.AmazonPay/favicon.png";
                string note;

                if (chargeback)
                {
                    note = T("Plugins.Payments.AmazonPay.IpnChargebackOrderNote",
                        messageId.NaIfEmpty(),
                        message.NotificationType, message.NotificationId,
                        message.ObjectType, message.ObjectId,
                        message.ChargePermissionId.NaIfEmpty());
                }
                else
                {
                    note = T("Plugins.Payments.AmazonPay.IpnOrderNote",
                        messageId.NaIfEmpty(),
                        message.NotificationType, message.NotificationId,
                        message.ObjectType, message.ObjectId,
                        message.ChargePermissionId.NaIfEmpty(),
                        oldState, newState.NullEmpty() ?? "-");
                }

                order.OrderNotes.Add(new OrderNote
                {
                    Note = $"<img src='{faviconUrl}' class='mr-1 align-text-top' />" + note,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                order.HasNewPaymentNotification = true;

                await _db.SaveChangesAsync();
            }
        }

        [Route("amazonpay/sharekey")]
        public async Task<IActionResult> ShareKey(string payload)
        {
            Response.Headers.Add("Access-Control-Allow-Origin", "https://payments.amazon.com");
            Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST");
            Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                await _amazonPayService.UpdateAccessKeysAsync(payload, 0);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 400;
                return Json(new { result = "error", message = ex.Message });
            }

            return Json(new { result = "success" });
        }

        #region Authentication\Sign-in

        /// <summary>
        /// AJAX. Creates the AmazonPay sign-in session object after clicking the AmazonPay button.
        /// </summary>
        [HttpPost]
        public IActionResult CreateSignInSession(string returnUrl)
        {
            var signature = string.Empty;
            var payload = string.Empty;
            var message = string.Empty;
            var messageType = string.Empty;

            try
            {
                var store = Services.StoreContext.CurrentStore;
                var currentScheme = Services.WebHelper.IsCurrentConnectionSecured() ? "https" : "http";
                var client = HttpContext.GetAmazonPayApiClient(store.Id);
                var signInReturnUrl = Url.Action(nameof(SignIn), "AmazonPay", null, currentScheme);

                var request = new SignInRequest(signInReturnUrl, _settings.ClientId)
                {
                    SignInScopes = new[]
                    {
                        SignInScope.Name,
                        SignInScope.Email,
                        //SignInScope.PostalCode, 
                        //SignInScope.ShippingAddress,
                        //SignInScope.BillingAddress,
                        //SignInScope.PhoneNumber
                    }
                };

                payload = request.ToJsonNoType();
                signature = client.GenerateButtonSignature(payload);

                HttpContext.Session.SetString("AmazonPayReturnUrl", returnUrl);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                messageType = "error";
            }

            return Json(new 
            {
                success = signature.HasValue(), 
                signature, 
                payload, 
                message,
                messageType
            });
        }

        /// <summary>
        /// The buyer is redirected to this action method after they click the sign-in button.
        /// </summary>
        [Route("amazonpay/signin")]
        [Authorize(AuthenticationSchemes = "Smartstore.AmazonPay")]
        public IActionResult SignIn(/*string buyerToken*/)
        {
            var returnUrl = HttpContext.Session.GetString("AmazonPayReturnUrl");

            return RedirectToAction(nameof(IdentityController.ExternalLoginCallback), "Identity", 
                new { provider = AmazonPaySignInProvider.SystemName, returnUrl });
        }

        #endregion
    }
}
