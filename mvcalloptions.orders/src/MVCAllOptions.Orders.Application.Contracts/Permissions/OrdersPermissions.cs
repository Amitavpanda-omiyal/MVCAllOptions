using Volo.Abp.Reflection;

namespace MVCAllOptions.Orders.Permissions;

public class OrdersPermissions
{
    public const string GroupName = "Orders";

    public static class Orders
    {
        public const string Default = GroupName + ".Orders";
        public const string Create  = Default + ".Create";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(OrdersPermissions));
    }
}
