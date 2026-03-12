using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Extensions;
using DocumentFormat.OpenXml.Spreadsheet;
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
        public void Api_admin_no_orgs_returns_org_id_0_org_type_null_same_source_userName()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user, "admin check");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType,Is.EqualTo(null));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(user));
        }

        [Test]
        public void Api_admin_with_multiple_org_ids_returns_orgType_null_org_id_0_same_source_userName()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user, "admin", "local_authority:894", "multi_academy_trust:2044");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(null));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(user));
        }

        [Test]
        public void Api_admin_with_local_authority_id_returns_local_authority_id_same_source_userName()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user, "admin","local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(user));
        }

        [Test]
        public void Api_end_user_with_local_authority_id_returns_local_authority_id_same_source_userName()
        {
            string user = "Nunit-admin";
            var principal = CreatePrincipal(user, "local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(user));
        }

        [Test]
        public void Api_fsm_admin_portal_with_global_local_authority_scope_returns_local_authority_id_0_same_userName_and_source()
        {
            string user = "free-school-meals-admin";
            var principal = CreatePrincipal(user, "local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(null));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(user));
        }

        [Test]
        public void Api_fsm_admin_portal_with_local_authority_id_returns_userName_local_authority_id()
        {
            string user = "free-school-meals-admin:Nunit.test@test.co.uk";
            var principal = CreatePrincipal(user, "local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo("Nunit.test@test.co.uk"));
            Assert.That(meta.Source, Is.EqualTo("free-school-meals-admin"));
        }

        [Test]
        public void Api_fsm_admin_portal__with_local_authority_id_returns_userName_multi_academy_trust_id()
        {
            string user = "free-school-meals-admin:Nunit.test@test.co.uk";
            var principal = CreatePrincipal(user, "multi_academy_trust:2044", "local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.multi_academy_trust));
            Assert.That(meta.OrganisationID, Is.EqualTo(2044));
            Assert.That(meta.UserName, Is.EqualTo("Nunit.test@test.co.uk"));
            Assert.That(meta.Source, Is.EqualTo("free-school-meals-admin"));
        }

        [Test]
        public void Api_fsm_admin_portal_establishment_id_returns_userName_establishment_id()
        {
            string user = "free-school-meals-admin:Nunit.test@test.co.uk";
            var principal = CreatePrincipal(user, "establishment:10000", "local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.establishment));
            Assert.That(meta.OrganisationID, Is.EqualTo(10000));
            Assert.That(meta.UserName, Is.EqualTo("Nunit.test@test.co.uk"));
            Assert.That(meta.Source, Is.EqualTo("free-school-meals-admin"));
        }

        [Test]
        public void Api_fsm_parent_portal_no_org_ids_returns_returns_local_authority_id_0_same_userName_and_source()
        {
            string user = "free-school-meals-frontend";
            var principal = CreatePrincipal(user, "establishment local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(null));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(user));
        }

        [Test]
        public void Api_childcare_admin_portal_no_org_id_returns_local_authority_id_0_same_userName_and_source()
        {
            string user = "childcare-admin";
            var principal = CreatePrincipal(user, "local_authority");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(null));
            Assert.That(meta.OrganisationID, Is.EqualTo(0));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(user));
        }

        [Test]
        public void Api_childcare_admin_portal__with_local_authority_id_returns_local_authority_id_same_userName_and_source()
        {
            string user = "childcare-admin";
            var principal = CreatePrincipal(user, "local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo(user));
            Assert.That(meta.Source, Is.EqualTo(user));
        }

        [Test]
        public void Api_childcare_admin_portal_with_local_authority_id_returns_userName_local_authority_id()
        {
            string user = "childcare-admin:Nunit.test@test.co.uk";
            var principal = CreatePrincipal(user, "local_authority:894");
            var meta = principal.CalculateMetaData();
            Assert.That(meta.OrganisationType, Is.EqualTo(OrganisationType.local_authority));
            Assert.That(meta.OrganisationID, Is.EqualTo(894));
            Assert.That(meta.UserName, Is.EqualTo("Nunit.test@test.co.uk"));
            Assert.That(meta.Source, Is.EqualTo("childcare-admin"));
        }
    }
}