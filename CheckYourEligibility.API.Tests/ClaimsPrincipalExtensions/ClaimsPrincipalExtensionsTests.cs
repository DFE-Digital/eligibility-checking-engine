using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Extensions;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.Security.Claims;

namespace CheckYourEligibility.API.Tests.Extensions
{
    [TestFixture]
    public class ClaimsPrincipalExtensionsTests
    {
        private ClaimsPrincipal CreatePrincipal(string clientId, params string[] scopes)
        {
            var claims = new List<Claim>
            {
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", clientId)
            };
            if (scopes.Length > 0)
                claims.Add(new Claim("scope", string.Join(" ", scopes)));
            var identity = new ClaimsIdentity(claims, "mock");
            return new ClaimsPrincipal(identity);
        }

        [Test]
        public void Api_admin_returns_orgID_0_unspecified()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user, "admin");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType,Is.EqualTo(OrganisationType.unspecified));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.api_admin));
        }

        [Test]
        public void Api_admin_with_multiple_orgIDs_returns_ambiguous_orgID_0()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user, "admin", "local_authority:894", "multi_academy_trust:2044");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.ambiguous));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.api_admin));
        }

        [Test]
        public void Api_admin_returns_local_authority_id()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user, "admin","local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.api_admin));
        }

        [Test]
        public void Api_end_user_returns_local_authority_id()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user, "local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.api_enduser));
        }

        [Test]
        public void Api_end_user_returns_local_authority_id_0()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user,"local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.api_enduser));
        }

        [Test]
        public void Api_fsm_admin_portal_returns_userName_null_local_authority_0()
        {
            string user = "free-school-meals-admin";
            var principal = CreatePrincipal(user, "local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(null));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.fsm_admin_portal));
        }

        [Test]
        public void Api_fsm_admin_portal_returns_userName_local_authority_id()
        {
            string user = "free-school-meals-admin:Nunit.test@test.co.uk";
            var principal = CreatePrincipal(user, "local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo("Nunit.test@test.co.uk"));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.fsm_admin_portal));
        }

        [Test]
        public void Api_fsm_admin_portal_returns_userName_multi_academy_trust_id()
        {
            string user = "free-school-meals-admin:Nunit.test@test.co.uk";
            var principal = CreatePrincipal(user, "multi_academy_trust:2044", "local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.multi_academy_trust));
            Assert.That(meta.OrganisationID, Is.EqualTo(2044));
            Assert.That(meta.UserName, Is.EqualTo("Nunit.test@test.co.uk"));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.fsm_admin_portal));
        }

        [Test]
        public void Api_fsm_admin_portal_returns_userName_establishment_id()
        {
            string user = "free-school-meals-admin:Nunit.test@test.co.uk";
            var principal = CreatePrincipal(user, "establishment:10000", "local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.establishment));
            Assert.That(meta.OrganisationID, Is.EqualTo(10000));
            Assert.That(meta.UserName, Is.EqualTo("Nunit.test@test.co.uk"));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.fsm_admin_portal));
        }

        [Test]
        public void Api_fsm_parent_portal_returns_userName_null_establishment_0()
        {
            string user = "free-school-meals-frontend";
            var principal = CreatePrincipal(user, "establishment");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.establishment));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(null));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.fsm_parent_portal));
        }

        [Test]
        public void Api_childcare_admin_portal_returns_userName_null_local_authority_0()
        {
            string user = "childcare-admin";
            var principal = CreatePrincipal(user, "local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(null));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.childcare_admin_portal));
        }

        [Test]
        public void Api_childcare_admin_portal_returns_userName_null_local_authority_id()
        {
            string user = "childcare-admin";
            var principal = CreatePrincipal(user, "local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo(null));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.childcare_admin_portal));
        }

        [Test]
        public void Api_childcare_admin_portal_returns_userName_local_authority_id()
        {
            string user = "childcare-admin:Nunit.test@test.co.uk";
            var principal = CreatePrincipal(user, "local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo("Nunit.test@test.co.uk"));
            Assert.That(meta.Source, Is.EqualTo(CheckSource.childcare_admin_portal));
        }

        //[Test]
        //public void SingleEstablishmentScopeWithId()
        //{
        //    var principal = CreatePrincipal("clientid", "establishment:789");
        //    var meta = principal.CalculateMetaData();
        //    Assert.AreEqual(OrganisationType.establishment, meta.OrganisationType);
        //    Assert.AreEqual(789, meta.OrganisationID);
        //}

        //[Test]
        //public void GeneralScopeOnly_ReturnsZeroId()
        //{
        //    var principal = CreatePrincipal("clientid", "local_authority");
        //    var meta = principal.CalculateMetaData();
        //    Assert.AreEqual(OrganisationType.local_authority, meta.OrganisationType);
        //    Assert.AreEqual(0, meta.OrganisationID);
        //}

        //[Test]
        //public void InvalidClientId_Throws()
        //{
        //    var claims = new List<Claim>();
        //    var identity = new ClaimsIdentity(claims, "mock");
        //    var principal = new ClaimsPrincipal(identity);
        //    Assert.Throws<System.NullReferenceException>(() => principal.CalculateMetaData());
        //}

        //[Test]
        //public void SourceAndUserName_ParentPortal()
        //{
        //    var principal = CreatePrincipal("free-school-meals-frontend:username", "local_authority:1");
        //    var meta = principal.CalculateMetaData();
        //    Assert.AreEqual(CheckSource.fsm_parent_portal, meta.Source);
        //    Assert.AreEqual("username", meta.UserName);
        //}

        //[Test]
        //public void SourceAndUserName_AdminPortal()
        //{
        //    var principal = CreatePrincipal("free-school-meals-admin:adminuser", "local_authority:1");
        //    var meta = principal.CalculateMetaData();
        //    Assert.AreEqual(CheckSource.fsm_admin_portal, meta.Source);
        //    Assert.AreEqual("adminuser", meta.UserName);
        //}

        //[Test]
        //public void SourceAndUserName_ChildcareAdmin()
        //{
        //    var principal = CreatePrincipal("childcare-admin:ccadmin", "local_authority:1");
        //    var meta = principal.CalculateMetaData();
        //    Assert.AreEqual(CheckSource.childcare_admin_portal, meta.Source);
        //    Assert.AreEqual("ccadmin", meta.UserName);
        //}

        //[Test]
        //public void SourceAndUserName_SupportPortal()
        //{
        //    var principal = CreatePrincipal("eligibility-checking-engine-support:support", "local_authority:1");
        //    var meta = principal.CalculateMetaData();
        //    Assert.AreEqual(CheckSource.support_portal, meta.Source);
        //    Assert.AreEqual("support", meta.UserName);
        //}

        //[Test]
        //public void SourceAndUserName_ApiAdmin()
        //{
        //    var principal = CreatePrincipal("adminclientid", "admin");
        //    var meta = principal.CalculateMetaData();
        //    Assert.AreEqual(CheckSource.api_admin, meta.Source);
        //    Assert.AreEqual("adminclientid", meta.UserName);
        //}

        //[Test]
        //public void SourceAndUserName_ApiEndUser()
        //{
        //    var principal = CreatePrincipal("enduserclientid", "local_authority:1");
        //    var meta = principal.CalculateMetaData();
        //    Assert.AreEqual(CheckSource.api_enduser, meta.Source);
        //    Assert.AreEqual("enduserclientid", meta.UserName);
        //}
    }
}