﻿using SAML2.DotNet35.Config;
using SAML2.DotNet35.Logging;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SAML2.DotNet35.Utils
{
    public class MetadataUtils
    {
        private readonly Saml2Configuration configuration;
        private readonly IInternalLogger logger;

        public MetadataUtils(Config.Saml2Configuration configuration, Logging.IInternalLogger logger)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (logger == null) throw new ArgumentNullException("logger");
            this.configuration = configuration;
            this.logger = logger;
        }

        /// <summary>
        /// Creates the metadata document.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="sign">if set to <c>true</c> sign the document.</param>
        public string CreateMetadataDocument(Encoding encoding, bool sign)
        {
            logger.Debug(TraceMessages.MetadataDocumentBeingCreated);

            var keyinfo = new System.Security.Cryptography.Xml.KeyInfo();
            var keyClause = new System.Security.Cryptography.Xml.KeyInfoX509Data(configuration.ServiceProvider.SigningCertificate, X509IncludeOption.EndCertOnly);
            keyinfo.AddClause(keyClause);

            var doc = new Saml20MetadataDocument(configuration, keyinfo, sign);

            logger.Debug(TraceMessages.MetadataDocumentCreated);
            return doc.ToXml(encoding, configuration.ServiceProvider.SigningCertificate);
        }
    }
}