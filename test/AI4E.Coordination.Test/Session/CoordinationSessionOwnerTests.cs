using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Coordination.Mocks;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Coordination.Session
{
    [TestClass]
    public class CoordinationSessionOwnerTests
    {
        public DateTimeProviderMock DateTimeProvider { get; set; }
        public SessionManagerMock SessionManager { get; set; }
        public SessionIdentifierProviderMock SessionProvider { get; set; }
        public IOptions<CoordinationManagerOptions> OptionsAccessor { get; set; }
        public LoggerMock<SessionOwner> Logger { get; set; }
        public SessionOwner CoordinationSessionOwner { get; set; }

        [TestInitialize]
        public void Setup()
        {
            DateTimeProvider = new DateTimeProviderMock();
            SessionManager = new SessionManagerMock(DateTimeProvider);
            SessionProvider = new SessionIdentifierProviderMock();
            OptionsAccessor = Options.Create(new CoordinationManagerOptions
            {
                LeaseLength = TimeSpan.FromMilliseconds(6)
            });
            Logger = new LoggerMock<SessionOwner>();
            CoordinationSessionOwner = new SessionOwner(
                SessionManager, SessionProvider, DateTimeProvider, OptionsAccessor, Logger);
        }

        [TestCleanup]
        public void TearDown()
        {
            CoordinationSessionOwner.Dispose();
        }

        [TestMethod]
        public async Task StartSessionTest()
        {
            var session = await CoordinationSessionOwner.GetSessionIdentifierAsync(default);

            Assert.AreEqual(SessionProvider.CreatedSessions.Single(), session);
            Assert.IsTrue(await SessionManager.IsAliveAsync(session));
        }

        [TestMethod]
        public async Task UpdateSessionTest()
        {
            await CoordinationSessionOwner.GetSessionIdentifierAsync(default);

            for (var i = 0; i < 100; i++)
            {
                // Simulate a time shift
                DateTimeProvider.CurrentTime += TimeSpan.FromMilliseconds(1);
                await Task.Delay(1);
            }

            var session = await CoordinationSessionOwner.GetSessionIdentifierAsync(default);

            Assert.IsTrue(await SessionManager.IsAliveAsync(session));
        }

        [TestMethod]
        public async Task TerminateSessionTest()
        {
            var session = await CoordinationSessionOwner.GetSessionIdentifierAsync(default);

            CoordinationSessionOwner.Dispose();

            await Task.Delay(20);

            Assert.IsFalse(await SessionManager.IsAliveAsync(session));
        }
    }
}
