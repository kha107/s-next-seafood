using System.Text.Json;
using SNextSeafood.Application.DTOs;

namespace SNextSeafood.Web.Helpers;

public static class SessionCartHelper
{
    private const string CartSessionKey = "SNEXT_SEAFOOD_CART";
    private const string VoucherSessionKey = "SNEXT_SEAFOOD_VOUCHER";
    private const string AuthorizedOrdersSessionKey = "SNEXT_AUTHORIZED_ORDERS";

    public static List<CartItemDto> GetCart(ISession session)
    {
        var json = session.GetString(CartSessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CartItemDto>();
        }
        return JsonSerializer.Deserialize<List<CartItemDto>>(json) ?? new List<CartItemDto>();
    }

    public static void SaveCart(ISession session, List<CartItemDto> cart)
    {
        session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
    }

    public static void ClearCart(ISession session)
    {
        session.Remove(CartSessionKey);
        session.Remove(VoucherSessionKey);
    }

    public static string? GetAppliedVoucher(ISession session)
    {
        return session.GetString(VoucherSessionKey);
    }

    public static void SetAppliedVoucher(ISession session, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            session.Remove(VoucherSessionKey);
        }
        else
        {
            session.SetString(VoucherSessionKey, code.Trim().ToUpperInvariant());
        }
    }

    public static void AuthorizeOrderInSession(ISession session, string orderCode)
    {
        if (string.IsNullOrWhiteSpace(orderCode)) return;
        var list = GetAuthorizedOrders(session);
        var normalized = orderCode.Trim().ToUpperInvariant();
        if (!list.Contains(normalized))
        {
            list.Add(normalized);
            session.SetString(AuthorizedOrdersSessionKey, JsonSerializer.Serialize(list));
        }
    }

    public static bool IsOrderAuthorizedInSession(ISession session, string orderCode)
    {
        if (string.IsNullOrWhiteSpace(orderCode)) return false;
        var list = GetAuthorizedOrders(session);
        return list.Contains(orderCode.Trim().ToUpperInvariant());
    }

    private static List<string> GetAuthorizedOrders(ISession session)
    {
        var json = session.GetString(AuthorizedOrdersSessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }
}
