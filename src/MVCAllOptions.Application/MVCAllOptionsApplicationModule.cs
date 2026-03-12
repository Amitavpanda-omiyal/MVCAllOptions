using Volo.Payment;
using Volo.Payment.Admin;
using Volo.Forms;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AuditLogging;
using Volo.Abp.Gdpr;
using Volo.Abp.LanguageManagement;
using Volo.FileManagement;
using Volo.Abp.OpenIddict;
using Volo.Abp.TextTemplateManagement;
using Volo.Saas.Host;
using Volo.CmsKit;
using Volo.Chat;
using Volo.AIManagement;
using Volo.AIManagement.Client;

namespace MVCAllOptions;

[DependsOn(
    typeof(AbpPaymentApplicationModule),
    typeof(AbpPaymentAdminApplicationModule),
    typeof(FormsApplicationModule),
    typeof(MVCAllOptionsDomainModule),
    typeof(MVCAllOptionsApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpAccountPublicApplicationModule),
    typeof(AbpAccountAdminApplicationModule),
    typeof(SaasHostApplicationModule),
    typeof(ChatApplicationModule),
    typeof(AbpAuditLoggingApplicationModule),
    typeof(TextTemplateManagementApplicationModule),
    typeof(AbpOpenIddictProApplicationModule),
    typeof(LanguageManagementApplicationModule),
    typeof(FileManagementApplicationModule),
    typeof(AbpGdprApplicationModule),
    typeof(CmsKitProApplicationModule),
    typeof(AIManagementApplicationModule),
    typeof(AIManagementClientApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class MVCAllOptionsApplicationModule : AbpModule
{
}
