using Volo.Payment.Iyzico;
using Volo.Payment.Stripe;
using Volo.Payment.PayPal;
using Volo.Payment.TwoCheckout;
using Volo.Payment.Payu;
using Volo.Payment;
using Volo.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Linq;
using MVCAllOptions.Localization;
using MVCAllOptions.MultiTenancy;
using System;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.BlobStoring.Database;
using Volo.Abp.Caching;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Commercial.SuiteTemplates;
using Volo.Abp.LanguageManagement;
using Volo.FileManagement;
using Volo.Abp.TextTemplateManagement;
using Volo.Saas;
using Volo.Abp.Gdpr;
using Volo.Chat;
using Volo.CmsKit;
using Volo.CmsKit.Contact;
using Volo.CmsKit.Newsletters;
using Microsoft.Extensions.AI;
using Volo.Abp.AI;
using Volo.AIManagement;
using Volo.AIManagement.Embeddings;
using Volo.AIManagement.Factory;
using Volo.AIManagement.OpenAI;
using Volo.AIManagement.OpenAI.Embeddings;
using Volo.AIManagement.Ollama;
using Volo.AIManagement.Ollama.Embeddings;
using Volo.AIManagement.VectorStores;
using Volo.AIManagement.VectorStores.Pgvector;
using Volo.AIManagement.DocumentProcessing;
using MVCAllOptions.DocumentProcessing;

namespace MVCAllOptions;

[DependsOn(
    typeof(AbpPaymentIyzicoDomainModule),
    typeof(AbpPaymentStripeDomainModule),
    typeof(AbpPaymentPayPalDomainModule),
    typeof(AbpPaymentTwoCheckoutDomainModule),
    typeof(AbpPaymentPayuDomainModule),
    typeof(AbpPaymentDomainModule),
    typeof(FormsDomainModule),
    typeof(MVCAllOptionsDomainSharedModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpFeatureManagementDomainModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpSettingManagementDomainModule),
    typeof(AbpEmailingModule),
    typeof(AbpIdentityProDomainModule),
    typeof(AbpOpenIddictProDomainModule),
    typeof(SaasDomainModule),
    typeof(ChatDomainModule),
    typeof(TextTemplateManagementDomainModule),
    typeof(LanguageManagementDomainModule),
    typeof(FileManagementDomainModule),
    typeof(VoloAbpCommercialSuiteTemplatesModule),
    typeof(AbpGdprDomainModule),
    typeof(CmsKitProDomainModule),
    typeof(AIManagementDomainModule),
    typeof(AIManagementOpenAIModule),
    typeof(AIManagementOllamaModule),
    typeof(AIManagementPgvectorModule),
    typeof(BlobStoringDatabaseDomainModule)
    )]
public class MVCAllOptionsDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = MultiTenancyConsts.IsEnabled;
        });

        Configure<NewsletterOptions>(options =>
        {
            options.AddPreference(
                "Newsletter_Default",
                new NewsletterPreferenceDefinition(
                    LocalizableString.Create<MVCAllOptionsResource>("NewsletterPreference_Default"),
                    privacyPolicyConfirmation: LocalizableString.Create<MVCAllOptionsResource>("NewsletterPrivacyAcceptMessage")
                )
            );
        });

        Configure<EmbeddingClientFactoryOptions>(options =>
        {
            options.AddFactory<OllamaEmbeddingClientFactory>("Ollama");
            options.AddFactory<OpenAIEmbeddingClientFactory>("OpenAI");
        });

        Configure<VectorStoreFactoryOptions>(options =>
        {
            options.AddFactory<PgvectorStoreFactory>("Pgvector");
        });

#if DEBUG
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif
    }

    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        // Replace the built-in PdfExtractor with SanitizedPdfExtractor to prevent
        // PostgreSQL 22P05 errors caused by null bytes / control characters in
        // technical PDFs (e.g. NFPA standards, scanned documents).
        var pdfDescriptor = context.Services
            .FirstOrDefault(sd =>
                sd.ServiceType == typeof(IDocumentTextExtractor) &&
                sd.ImplementationType == typeof(PdfExtractor));

        if (pdfDescriptor != null)
            context.Services.Remove(pdfDescriptor);

        // Keep PdfExtractor available as a concrete type so SanitizedPdfExtractor can inject it.
        context.Services.TryAddTransient<PdfExtractor>();
        context.Services.AddTransient<IDocumentTextExtractor, SanitizedPdfExtractor>();
    }
}
