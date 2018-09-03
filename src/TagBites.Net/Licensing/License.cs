using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace TagBites.Net.Licensing
{
    class License
    {
        public string Id { get; private set; }
        public string Type { get; private set; }
        public string ProductName { get; private set; }
        public DateTime SubscriptionUntil { get; private set; }
        public string RegisteredTo { get; private set; }

        private byte[] Hash { get; set; }
        private byte[] Signature { get; set; }

        private License()
        { }


        public bool VerifySignature(string licenseVerifyKey)
        {
            if (Signature == null)
                throw new InvalidOperationException("License was not signed.");

            using (var dsa = new DSACryptoServiceProvider())
            {
                var blob = Convert.FromBase64String(licenseVerifyKey);
                dsa.ImportCspBlob(blob);
                return dsa.VerifySignature(Hash, Signature);
            }
        }

        public static License FromXml(string xml)
        {
            var license = new License();

            // Parse
            var doc = XDocument.Parse(xml);
            var data = doc.Root?.Element("Data");
            var signature = (string)doc.Root?.Element("Signature");

            if (data == null
                || string.IsNullOrEmpty(license.Id = (string)data.Element("Id"))
                || string.IsNullOrEmpty(license.Type = (string)data.Element("Type"))
                || string.IsNullOrEmpty(license.ProductName = (string)data.Element("ProductName"))
                || string.IsNullOrEmpty(license.RegisteredTo = (string)data.Element("RegisteredTo"))
                || string.IsNullOrEmpty(signature)
                || !DateTime.TryParse((string)data.Element("SubscriptionUntil"), out var subscriptionUntil))
            {
                ThrowInvalidLicenseException();
                return null;
            }

            // SubscriptionUntil
            license.SubscriptionUntil = subscriptionUntil;

            // Hash
            using (var sha1 = SHA1.Create())
            {
                var dataString = data.ToString(SaveOptions.DisableFormatting);
                var buffer = Encoding.UTF8.GetBytes(dataString);
                license.Hash = sha1.ComputeHash(buffer);
            }

            // Signature
            license.Signature = Convert.FromBase64String(signature);

            return license;
        }
        public static License FromCompressedBase64Xml(string compressedBase64Xml)
        {
            try
            {
                using (var source = new MemoryStream(Convert.FromBase64String(compressedBase64Xml)))
                using (var destination = new MemoryStream())
                {
                    using (var gz = new GZipStream(source, CompressionMode.Decompress))
                        gz.CopyTo(destination);

                    compressedBase64Xml = Encoding.UTF8.GetString(destination.ToArray());
                }
            }
            catch
            {
                ThrowInvalidLicenseException();
            }

            return FromXml(compressedBase64Xml);
        }

        private static void ThrowInvalidLicenseException() => throw new Exception("Invalid license.");
    }
}
