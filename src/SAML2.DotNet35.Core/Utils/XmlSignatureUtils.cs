using SAML2.DotNet35.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace SAML2.DotNet35.Utils
{
    /// <summary>
    /// This class contains methods that creates and validates signatures on XmlDocuments.
    /// </summary>
    public class XmlSignatureUtils
    {
        #region Public methods

        /// <summary>
        /// Verifies the signature of the XmlDocument instance using the key enclosed with the signature.
        /// </summary>
        /// <param name="doc">The doc.</param>
        /// <returns><code>true</code> if the document's signature can be verified. <code>false</code> if the signature could
        /// not be verified.</returns>
        /// <exception cref="InvalidOperationException">if the XmlDocument instance does not contain a signed XML document.</exception>
        public static bool CheckSignature(XmlDocument doc)
        {
            CheckDocument(doc);
            var signedXml = RetrieveSignature(doc);

            if (signedXml.SignatureMethod.Contains("rsa-sha256"))
            {
                // SHA256 keys must be obtained from message manually
                var trustedCertificates = GetCertificates(doc);
                foreach (var cert in trustedCertificates)
                {
                    if (signedXml.CheckSignature(cert.PublicKey.Key))
                    {
                        return true;
                    }
                }

                return false;
            }

            return signedXml.CheckSignature();
        }

        /// <summary>
        /// Verifies the signature of the XmlDocument instance using the key given as a parameter.
        /// </summary>
        /// <param name="doc">The doc.</param>
        /// <param name="alg">The algorithm.</param>
        /// <returns><code>true</code> if the document's signature can be verified. <code>false</code> if the signature could
        /// not be verified.</returns>
        /// <exception cref="InvalidOperationException">if the XmlDocument instance does not contain a signed XML document.</exception>
        public static bool CheckSignature(XmlDocument doc, AsymmetricAlgorithm alg)
        {
            CheckDocument(doc);
            var signedXml = RetrieveSignature(doc);

            return signedXml.CheckSignature(alg);
        }

        /// <summary>
        /// Verifies the signature of the XmlElement instance using the key given as a parameter.
        /// </summary>
        /// <param name="el">The element.</param>
        /// <param name="alg">The algorithm.</param>
        /// <returns><code>true</code> if the element's signature can be verified. <code>false</code> if the signature could
        /// not be verified.</returns>
        /// <exception cref="InvalidOperationException">if the XmlDocument instance does not contain a signed XML element.</exception>
        public static bool CheckSignature(XmlElement el, AsymmetricAlgorithm alg)
        {
            // CheckDocument(element);
            var signedXml = RetrieveSignature(el);

            return signedXml.CheckSignature(alg);
        }

        /// <summary>
        /// Verify the given document using a KeyInfo instance. The KeyInfo instance's KeyClauses will be traversed for
        /// elements that can verify the signature, e.g. certificates or keys. If nothing is found, an exception is thrown.
        /// </summary>
        /// <param name="doc">The doc.</param>
        /// <param name="keyinfo">The key info.</param>
        /// <returns><code>true</code> if the element's signature can be verified. <code>false</code> if the signature could
        /// not be verified.</returns>
        public static bool CheckSignature(XmlDocument doc, KeyInfo keyinfo)
        {
            CheckDocument(doc);
            var signedXml = RetrieveSignature(doc);

            AsymmetricAlgorithm alg = null;
            X509Certificate2 cert = null;
            foreach (KeyInfoClause clause in keyinfo)
            {
                if (clause is RSAKeyValue)
                {
                    var key = (RSAKeyValue)clause;
                    alg = key.Key;
                    break;
                }

                if (clause is KeyInfoX509Data)
                {
                    var x509Data = (KeyInfoX509Data)clause;
                    var count = x509Data.Certificates.Count;
                    cert = (X509Certificate2)x509Data.Certificates[count - 1];
                }
                else if (clause is DSAKeyValue)
                {
                    var key = (DSAKeyValue)clause;
                    alg = key.Key;
                    break;
                }
            }

            if (alg == null && cert == null)
            {
                throw new InvalidOperationException("Unable to locate the key or certificate to verify the signature.");
            }

            return alg != null ? signedXml.CheckSignature(alg) : signedXml.CheckSignature(cert, true);
        }

        /// <summary>
        /// Attempts to retrieve an asymmetric key from the KeyInfoClause given as parameter.
        /// </summary>
        /// <param name="keyInfoClause">The key info clause.</param>
        /// <returns>null if the key could not be found.</returns>
        public static AsymmetricAlgorithm ExtractKey(KeyInfoClause keyInfoClause)
        {
            if (keyInfoClause is RSAKeyValue)
            {
                var key = (RSAKeyValue)keyInfoClause;
                return key.Key;
            }

            if (keyInfoClause is KeyInfoX509Data)
            {
                var cert = GetCertificateFromKeyInfo((KeyInfoX509Data)keyInfoClause);
                return cert != null ? cert.PublicKey.Key : null;
            }

            if (keyInfoClause is DSAKeyValue)
            {
                var key = (DSAKeyValue)keyInfoClause;
                return key.Key;
            }

            return null;
        }

        /// <summary>
        /// Returns the KeyInfo element that is included with the signature in the document.
        /// </summary>
        /// <param name="doc">The doc.</param>
        /// <returns>The signature <see cref="KeyInfo"/>.</returns>
        /// <exception cref="InvalidOperationException">if the document is not signed.</exception>
        public static KeyInfo ExtractSignatureKeys(XmlDocument doc)
        {
            CheckDocument(doc);
            if (doc.DocumentElement != null)
            {
                var signedXml = new SignedXml(doc.DocumentElement);

                var nodeList = doc.GetElementsByTagName(Schema.XmlDSig.Signature.ElementName, Saml20Constants.Xmldsig);
                if (nodeList.Count == 0)
                {
                    throw new InvalidOperationException("The XmlDocument does not contain a signature.");
                }

                signedXml.LoadXml((XmlElement)nodeList[0]);

                return signedXml.KeyInfo;
            }

            return null;
        }

        /// <summary>
        /// Returns the KeyInfo element that is included with the signature in the element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The signature <see cref="KeyInfo"/>.</returns>
        /// <exception cref="InvalidOperationException">if the document is not signed.</exception>
        public static KeyInfo ExtractSignatureKeys(XmlElement element)
        {
            CheckDocument(element);
            var signedXml = new SignedXml(element);

            var nodeList = element.GetElementsByTagName(Schema.XmlDSig.Signature.ElementName, Saml20Constants.Xmldsig);
            if (nodeList.Count == 0)
            {
                throw new InvalidOperationException("The XmlDocument does not contain a signature.");
            }

            signedXml.LoadXml((XmlElement)nodeList[0]);

            return signedXml.KeyInfo;
        }

        /// <summary>
        /// Gets the certificate from key info.
        /// </summary>
        /// <param name="keyInfo">The key info.</param>
        /// <returns>The last certificate in the chain</returns>
        public static X509Certificate2 GetCertificateFromKeyInfo(KeyInfoX509Data keyInfo)
        {
            var count = keyInfo.Certificates.Count;
            if (count == 0)
            {
                return null;
            }

            var cert = (X509Certificate2)keyInfo.Certificates[count - 1];

            return cert;
        }

        /// <summary>
        /// Checks if a document contains a signature.
        /// </summary>
        /// <param name="doc">The doc.</param>
        /// <returns><c>true</c> if the specified doc is signed; otherwise, <c>false</c>.</returns>
        public static bool IsSigned(XmlDocument doc)
        {
            CheckDocument(doc);
            var nodeList = doc.GetElementsByTagName(Schema.XmlDSig.Signature.ElementName, Saml20Constants.Xmldsig);

            return nodeList.Count > 0;
        }

        /// <summary>
        /// Checks if an element contains a signature.
        /// </summary>
        /// <param name="el">The element.</param>
        /// <returns><c>true</c> if the specified element is signed; otherwise, <c>false</c>.</returns>
        public static bool IsSigned(XmlElement el)
        {
            CheckDocument(el);
            var nodeList = el.GetElementsByTagName(Schema.XmlDSig.Signature.ElementName, Saml20Constants.Xmldsig);

            return nodeList.Count > 0;
        }

        /// <summary>
        /// Signs an XmlDocument with an xml signature using the signing certificate specified in the
        /// configuration file.
        /// </summary>
        /// <param name="doc">The XmlDocument to be signed</param>
        /// <param name="id">The id of the topmost element in the XmlDocument</param>
        public static void SignDocument(XmlDocument doc, string id, Saml2Configuration config)
        {
            SignDocument(doc, id, config.ServiceProvider.SigningCertificate, config);
        }

        /// <summary>
        /// Signs an XmlDocument with an xml signature using the signing certificate given as argument to the method.
        /// </summary>
        /// <param name="doc">The XmlDocument to be signed</param>
        /// <param name="id">The id of the topmost element in the XmlDocument</param>
        /// <param name="cert">The certificate used to sign the document</param>
        public static void SignDocument(XmlDocument doc, string id, X509Certificate2 cert, Saml2Configuration samlConfiguration)
        {
            var signedXml = SetupSignedDocWithSignatureType(doc, cert, samlConfiguration);

            // Retrieve the value of the "ID" attribute on the root assertion element.
            var reference = new Reference("#" + id);

            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new XmlDsigExcC14NTransform());

            signedXml.AddReference(reference);

            // Include the public key of the certificate in the assertion.
            signedXml.KeyInfo = new KeyInfo();
            signedXml.KeyInfo.AddClause(new KeyInfoX509Data(cert, X509IncludeOption.WholeChain));

            signedXml.ComputeSignature();

            // Append the computed signature. The signature must be placed as the sibling of the Issuer element.
            if (doc.DocumentElement != null)
            {
                var nodes = doc.DocumentElement.GetElementsByTagName("Issuer", Saml20Constants.Assertion);

                var parentNode = nodes[0].ParentNode;
                if (parentNode != null)
                {
                    parentNode.InsertAfter(doc.ImportNode(signedXml.GetXml(), true), nodes[0]);
                }
            }
        }

        private static SignedXml SetupSignedDocWithSignatureType(XmlDocument doc, X509Certificate2 cert, Saml2Configuration samlConfiguration)
        {
            var signedXml = new SignedXml(doc);
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
            if (samlConfiguration.SigningAlgorithm == AlgorithmType.SHA256)
            {
                SetupSHA256();

                var exportedKeyMaterial = cert.PrivateKey.ToXmlString( /* includePrivateParameters = */ true);

                var cspParameters = new CspParameters(24 /* PROV_RSA_AES */);
                var key = new RSACryptoServiceProvider(cspParameters);
                key.PersistKeyInCsp = false;
                key.FromXmlString(exportedKeyMaterial);

                signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
                signedXml.SigningKey = key;
            }
            else if (samlConfiguration.SigningAlgorithm == AlgorithmType.SHA1)
            {
                signedXml.SigningKey = cert.PrivateKey;
            }
            else
            {
                throw new NotImplementedException(string.Format("Signing with algoritm {0} is not implemented", AlgorithmType.SHA512.ToString()));
            }

            return signedXml;
        }

        #endregion Public methods

        #region Private methods

        /// <summary>
        /// Do checks on the document given. Every public method accepting a XmlDocument instance as parameter should
        /// call this method before continuing.
        /// </summary>
        /// <param name="doc">The doc.</param>
        private static void CheckDocument(XmlDocument doc)
        {
            if (doc == null)
            {
                throw new ArgumentNullException("doc");
            }

            if (!doc.PreserveWhitespace)
            {
                throw new InvalidOperationException("The XmlDocument must have its \"PreserveWhitespace\" property set to true when a signed document is loaded.");
            }
        }

        /// <summary>
        /// Do checks on the element given. Every public method accepting a XmlElement instance as parameter should
        /// call this method before continuing.
        /// </summary>
        /// <param name="el">The element.</param>
        private static void CheckDocument(XmlElement el)
        {
            if (el == null)
            {
                throw new ArgumentNullException("el");
            }

            if (el.OwnerDocument != null && !el.OwnerDocument.PreserveWhitespace)
            {
                throw new InvalidOperationException("The XmlDocument must have its \"PreserveWhitespace\" property set to true when a signed document is loaded.");
            }
        }

        /// <summary>
        /// Gets the certificates.
        /// </summary>
        /// <param name="doc">The document.</param>
        /// <returns>List of <see cref="X509Certificate2"/>.</returns>
        private static List<X509Certificate2> GetCertificates(XmlDocument doc)
        {
            var certificates = new List<X509Certificate2>();
            var nodeList = doc.GetElementsByTagName("ds:X509Certificate");
            if (nodeList.Count == 0)
            {
                nodeList = doc.GetElementsByTagName("X509Certificate");
            }

            foreach (XmlNode xn in nodeList)
            {
                try
                {
                    var xc = new X509Certificate2(Convert.FromBase64String(xn.InnerText));
                    certificates.Add(xc);
                }
                catch
                {
                    // Swallow the certificate parse error
                }
            }

            return certificates;
        }

        /// <summary>
        /// Digs the &lt;Signature&gt; element out of the document.
        /// </summary>
        /// <param name="doc">The doc.</param>
        /// <returns>The <see cref="SignedXml"/>.</returns>
        /// <exception cref="InvalidOperationException">if the document does not contain a signature.</exception>
        private static SignedXml RetrieveSignature(XmlDocument doc)
        {
            return RetrieveSignature(doc.DocumentElement);
        }

        /// <summary>
        /// Digs the &lt;Signature&gt; element out of the document.
        /// </summary>
        /// <param name="el">The element.</param>
        /// <returns>The <see cref="SignedXml"/>.</returns>
        /// <exception cref="InvalidOperationException">if the document does not contain a signature.</exception>
        private static SignedXml RetrieveSignature(XmlElement el)
        {
            if (el.OwnerDocument.DocumentElement == null)
            {
                var doc = new XmlDocument() { PreserveWhitespace = true };
                doc.LoadXml(el.OuterXml);
                el = doc.DocumentElement;
            }

            SignedXml signedXml = new SignedXmlWithIdResolvement(el);
            var nodeList = el.GetElementsByTagName(Schema.XmlDSig.Signature.ElementName, Saml20Constants.Xmldsig);
            if (nodeList.Count == 0)
            {
                throw new InvalidOperationException("Document does not contain a signature to verify.");
            }

            signedXml.LoadXml((XmlElement)nodeList[0]);

            // To support SHA256 for XML signatures, an additional algorithm must be enabled.
            // This is not supported in .Net versions older than 4.0. In older versions,
            // an exception will be raised if an SHA256 signature method is attempted to be used.
            if (signedXml.SignatureMethod.Contains("rsa-sha256"))
            {
                SetupSHA256();
            }

            // verify that the inlined signature has a valid reference uri
            VerifyReferenceUri(signedXml, el.GetAttribute("ID"));

            return signedXml;
        }

        public static void SetupSHA256()
        {
            var addAlgorithmMethod = typeof(CryptoConfig).GetMethod("AddAlgorithm", BindingFlags.Public | BindingFlags.Static);
            if (addAlgorithmMethod == null)
            {
                var ob1 = CryptoConfig.CreateFromName("SHA256");
                AddAlgorithm("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256", typeof(RSAPKCS1SHA256SignatureDescription));               
            }
            else
            {
                addAlgorithmMethod.Invoke(null, new object[] { typeof(RSAPKCS1SHA256SignatureDescription), new[] { "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256" } });
            }
        }

        private static void AddAlgorithm(String key, object value)
        {
#if Version_4
                var defaultNameHT =
                typeof(CryptoConfig).GetField("defaultNameHT", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as Dictionary;
#endif

            var defaultNameHT =
            typeof(CryptoConfig).GetProperty("DefaultNameHT", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null, null) as System.Collections.Hashtable;

            if (!defaultNameHT.ContainsKey(key))
            {
                defaultNameHT.Add(key, value);
            }
        }

        /// <summary>
        /// Verifies that the reference uri (if any) points to the correct element.
        /// </summary>
        /// <param name="signedXml">the ds:signature element</param>
        /// <param name="id">the expected id referenced by the ds:signature element</param>
        private static void VerifyReferenceUri(SignedXml signedXml, string id)
        {
            if (id == null)
            {
                throw new InvalidOperationException("Cannot match null id");
            }

            if (signedXml.SignedInfo.References.Count <= 0)
            {
                throw new InvalidOperationException("No references in Signature element");
            }

            var reference = (Reference)signedXml.SignedInfo.References[0];
            var uri = reference.Uri;

            // empty uri is okay - indicates that everything is signed
            if (!string.IsNullOrEmpty(uri))
            {
                if (!uri.StartsWith("#"))
                {
                    throw new InvalidOperationException("Signature reference URI is not a document fragment reference. Uri = '" + uri + "'");
                }

                if (uri.Length < 2 || !id.Equals(uri.Substring(1)))
                {
                    throw new InvalidOperationException("Rererence URI = '" + uri.Substring(1) + "' does not match expected id = '" + id + "'");
                }
            }
        }

        #endregion

        /// <summary>
        /// Used to validate SHA256 signatures
        /// </summary>
        public class RSAPKCS1SHA256SignatureDescription : SignatureDescription
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="RSAPKCS1SHA256SignatureDescription"/> class.
            /// </summary>
            public RSAPKCS1SHA256SignatureDescription()
            {
                KeyAlgorithm = typeof(RSACryptoServiceProvider).FullName;
                DigestAlgorithm = typeof(SHA256Managed).FullName;   // Note - SHA256CryptoServiceProvider is not registered with CryptoConfig
                FormatterAlgorithm = typeof(RSAPKCS1SignatureFormatter).FullName;
                DeformatterAlgorithm = typeof(RSAPKCS1SignatureDeformatter).FullName;
            }

            /// <summary>
            /// Creates signature deformatter
            /// </summary>
            /// <param name="key">The key to use in the <see cref="T:System.Security.Cryptography.AsymmetricSignatureDeformatter" />.</param>
            /// <returns>The newly created <see cref="T:System.Security.Cryptography.AsymmetricSignatureDeformatter" /> instance.</returns>
            public override AsymmetricSignatureDeformatter CreateDeformatter(AsymmetricAlgorithm key)
            {
                if (key == null)
                    throw new ArgumentNullException("RSAPKCS1SHA256SignatureDescription AsymmetricAlgorithm param: key is null");

                RSAPKCS1SignatureDeformatter deformatter = new RSAPKCS1SignatureDeformatter(key);
                deformatter.SetHashAlgorithm("SHA256");
                return deformatter;
            }

            public override AsymmetricSignatureFormatter CreateFormatter(AsymmetricAlgorithm key)
            {
                if (key == null)
                    throw new ArgumentNullException("RSAPKCS1SHA256SignatureDescription AsymmetricAlgorithm param: key is null");

                RSAPKCS1SignatureFormatter formatter = new RSAPKCS1SignatureFormatter(key);
                formatter.SetHashAlgorithm("SHA256");
                return formatter;
            }
        }

        /// <summary>
        /// Signed XML with Id Resolvement class.
        /// </summary>
        public class SignedXmlWithIdResolvement : SignedXml
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SignedXmlWithIdResolvement"/> class.
            /// </summary>
            /// <param name="document">The document.</param>
            public SignedXmlWithIdResolvement(XmlDocument document) : base(document) { }

            /// <summary>
            /// Initializes a new instance of the <see cref="SignedXmlWithIdResolvement"/> class from the specified <see cref="T:System.Xml.XmlElement"/> object.
            /// </summary>
            /// <param name="elem">The <see cref="T:System.Xml.XmlElement"/> object to use to initialize the new instance of <see cref="T:System.Security.Cryptography.Xml.SignedXml"/>.</param>
            /// <exception cref="T:System.ArgumentNullException">
            /// The <paramref name="elem"/> parameter is null.
            /// </exception>
            public SignedXmlWithIdResolvement(XmlElement elem) : base(elem) { }

            /// <summary>
            /// Initializes a new instance of the <see cref="SignedXmlWithIdResolvement"/> class.
            /// </summary>
            public SignedXmlWithIdResolvement() { }

            /// <summary>
            /// Returns the <see cref="T:System.Xml.XmlElement"/> object with the specified ID from the specified <see cref="T:System.Xml.XmlDocument"/> object.
            /// </summary>
            /// <param name="document">The <see cref="T:System.Xml.XmlDocument"/> object to retrieve the <see cref="T:System.Xml.XmlElement"/> object from.</param>
            /// <param name="idValue">The ID of the <see cref="T:System.Xml.XmlElement"/> object to retrieve from the <see cref="T:System.Xml.XmlDocument"/> object.</param>
            /// <returns>The <see cref="T:System.Xml.XmlElement"/> object with the specified ID from the specified <see cref="T:System.Xml.XmlDocument"/> object, or null if it could not be found.</returns>
            public override XmlElement GetIdElement(XmlDocument document, string idValue)
            {
                var elem = base.GetIdElement(document, idValue);
                if (elem == null)
                {
                    var nl = document.GetElementsByTagName("*");
                    var enumerator = nl.GetEnumerator();
                    while (enumerator != null && enumerator.MoveNext())
                    {
                        var node = (XmlNode)enumerator.Current;
                        if (node == null || node.Attributes == null)
                        {
                            continue;
                        }

                        var nodeEnum = node.Attributes.GetEnumerator();
                        while (nodeEnum != null && nodeEnum.MoveNext())
                        {
                            var attr = (XmlAttribute)nodeEnum.Current;
                            if (attr != null && (attr.LocalName.ToLower() == "id" && attr.Value == idValue && node is XmlElement))
                            {
                                return (XmlElement)node;
                            }
                        }
                    }
                }

                return elem;
            }
        }
    }
}