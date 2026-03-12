using System.Threading.Tasks;
using MVCAllOptions.Orders.Permissions;
using Volo.Abp.UI.Navigation;

namespace MVCAllOptions.Orders.Web.Menus;

public class OrdersMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private async Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        if (await context.IsGrantedAsync(OrdersPermissions.Orders.Default))
        {
            context.Menu.AddItem(
                new ApplicationMenuItem(
                    OrdersMenus.Prefix,
                    displayName: "Orders",
                    url: "~/Orders",
                    icon: "fa fa-basket-shopping"));
        }
    }
}
