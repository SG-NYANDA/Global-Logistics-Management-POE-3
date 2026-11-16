using GLMS.Web.Data;
using GLMS.Web.Models;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GLMS.Tests;

public sealed class EmailVerificationServiceTests
{
    [Fact]
    public async Task ValidCodeConfirmsEmailAndConsumesChallenge()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.VerificationService.SendCodeAsync(
            harness.User,
            enforceResendInterval: false,
            CancellationToken.None);

        Assert.Equal(6, harness.EmailSender.LastCode?.Length);
        Assert.True(await harness.VerificationService.VerifyCodeAsync(
            harness.User,
            harness.EmailSender.LastCode!,
            CancellationToken.None));
        Assert.True(harness.User.EmailConfirmed);
        Assert.False(await harness.VerificationService.VerifyCodeAsync(
            harness.User,
            harness.EmailSender.LastCode!,
            CancellationToken.None));
    }

    [Fact]
    public async Task FifthIncorrectAttemptInvalidatesCode()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.VerificationService.SendCodeAsync(
            harness.User,
            enforceResendInterval: false,
            CancellationToken.None);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.False(await harness.VerificationService.VerifyCodeAsync(
                harness.User,
                "incorrect",
                CancellationToken.None));
        }

        Assert.False(await harness.VerificationService.VerifyCodeAsync(
            harness.User,
            harness.EmailSender.LastCode!,
            CancellationToken.None));
        Assert.Empty(await harness.Db.EmailVerificationChallenges.ToListAsync());
    }

    [Fact]
    public async Task ResendIsThrottledForOneMinute()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.VerificationService.SendCodeAsync(
            harness.User,
            enforceResendInterval: false,
            CancellationToken.None);

        await Assert.ThrowsAsync<VerificationResendTooSoonException>(() =>
            harness.VerificationService.SendCodeAsync(
                harness.User,
                enforceResendInterval: true,
                CancellationToken.None));
        Assert.Equal(1, harness.EmailSender.SendCount);
    }

    [Fact]
    public async Task ExpiredCodeIsRejectedAndRemoved()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.VerificationService.SendCodeAsync(
            harness.User,
            enforceResendInterval: false,
            CancellationToken.None);
        var challenge = await harness.Db.EmailVerificationChallenges.SingleAsync();
        challenge.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
        await harness.Db.SaveChangesAsync();

        Assert.False(await harness.VerificationService.VerifyCodeAsync(
            harness.User,
            harness.EmailSender.LastCode!,
            CancellationToken.None));
        Assert.Empty(await harness.Db.EmailVerificationChallenges.ToListAsync());
    }

    private sealed class TestHarness(
        ServiceProvider provider,
        GlmsDbContext db,
        ApplicationUser user,
        EmailVerificationService verificationService,
        RecordingEmailSender emailSender) : IDisposable
    {
        public GlmsDbContext Db { get; } = db;
        public ApplicationUser User { get; } = user;
        public EmailVerificationService VerificationService { get; } = verificationService;
        public RecordingEmailSender EmailSender { get; } = emailSender;

        public static async Task<TestHarness> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<GlmsDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>()
                .AddEntityFrameworkStores<GlmsDbContext>();

            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<GlmsDbContext>();
            await db.Database.EnsureCreatedAsync();

            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = "otp-test@example.com",
                Email = "otp-test@example.com"
            };
            var createResult = await userManager.CreateAsync(user, "Test@1234");
            Assert.True(createResult.Succeeded);

            var emailSender = new RecordingEmailSender();
            var verificationService = new EmailVerificationService(
                db,
                userManager,
                provider.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
                emailSender);
            return new TestHarness(provider, db, user, verificationService, emailSender);
        }

        public void Dispose() => provider.Dispose();
    }

    private sealed class RecordingEmailSender : IAccountEmailSender
    {
        public string? LastCode { get; private set; }
        public int SendCount { get; private set; }

        public Task SendVerificationCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken)
        {
            LastCode = code;
            SendCount++;
            return Task.CompletedTask;
        }
    }
}
