using System;
using SAML2.DotNet35.Schema.Core;

namespace SAML2.DotNet35.Validation
{
    /// <summary>
    /// SAML2 Assertion Validator interface.
    /// </summary>
    public interface ISaml20AssertionValidator
    {
        /// <summary>
        /// Validates the assertion.
        /// </summary>
        /// <param name="assertion">The assertion.</param>
        void ValidateAssertion(Assertion assertion, bool allowAnyAuthContextDeclRef);

        /// <summary>
        /// Validates the time restrictions.
        /// </summary>
        /// <param name="assertion">The assertion.</param>
        /// <param name="allowedClockSkew">The allowed clock skew.</param>
        void ValidateTimeRestrictions(Assertion assertion, TimeSpan allowedClockSkew);
    }
}
