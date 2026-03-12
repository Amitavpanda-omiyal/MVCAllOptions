using MVCAllOptions.Orders.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace MVCAllOptions.Orders.Permissions;

public class OrdersPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(OrdersPermissions.GroupName, L("Permission:Orders"));

        var orders = myGroup.AddPermission(OrdersPermissions.Orders.Default, L("Permission:Orders"));
        orders.AddChild(OrdersPermissions.Orders.Create, L("Permission:Orders.Create"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<OrdersResource>(name);
    }
}
