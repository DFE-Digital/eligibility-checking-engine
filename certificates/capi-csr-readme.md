# DWP CAPI client authentication certificate update process

## Generating CSRs to send to DWP

Run the this script:
```powershell
\eligibility-checking-engine\certificates\Generate-ClientAuthCSRs.ps1
```

It will generate the following folder structure and files:

```
DEV/
├── www.dev.eligibility-checking-engine.education.gov.uk.key   ← Private Key (SECURE!)
├── www.dev.eligibility-checking-engine.education.gov.uk.csr   ← Send to CA
└── www.dev.eligibility-checking-engine.education.gov.uk.conf  ← Reference only
TEST/
├── www.test.eligibility-checking-engine.education.gov.uk.key
├── www.test.eligibility-checking-engine.education.gov.uk.csr
└── www.test.eligibility-checking-engine.education.gov.uk.conf
PP/
├── www.pp.eligibility-checking-engine.education.gov.uk.key
├── www.pp.eligibility-checking-engine.education.gov.uk.csr
└── www.pp.eligibility-checking-engine.education.gov.uk.conf
PROD/
├── www.eligibility-checking-engine.education.gov.uk.key
├── www.eligibility-checking-engine.education.gov.uk.csr
└── www.eligibility-checking-engine.education.gov.uk.conf
```

### Certificate Properties

Each CSR has:
- **Key Size**: 2048-bit RSA
- **Signature**: SHA256 with RSA
- **Extended Key Usage**: Client Authentication Only

### Security Checklist

- Each environment has its own separate key pair
- `.key` files are NOT committed to git (checked by .gitignore)
- `.key` files should NOT be shared via email

## Sending CSRs to DWP

Complete the DWP 'front door request form' to get a service request reference.

https://forms.office.com/Pages/ResponsePage.aspx?id=6fbxllcQF0GsKIDN_ob4w8-xnpabh0NNoBkfcI7Q-SdUODlLR0xVWkdBM1VRWVNCOUhLMkY2RU9WQi4u

Respond to the service request from DWP with the generated `.csr` files. Keep the corresponding `.key` files secure and do not share them.

### Domain validation

The CA used by DWP will perform domain validation to ensure that the request is legitimate. This step is necessary to confirm that the requester has control over the domain for which the certificate is being requested.

DWP will provide a list of TXT records that need to be added for domain validation.  This is actioned using the External DNS - AWS Route53 service request:
https://dfe.service-now.com/mydfe?id=sc_cat_item&sys_id=898db1201bacaed0593ddce7b04bcb42&referrer=recent_items

An example of the request content is shown below:

| Domain | Record Type | Name | Value | TTL |
|--------|-------------|------|-------|-----|
| www.dev.eligibility-checking-engine.education.gov.uk | TXT Record | _t6cq2ncspwhmndrwipn8sv4m9c755vm | 3600 |
| www.test.eligibility-checking-engine.education.gov.uk | TXT Record | _e0g3qhave2yhv242l58ddzebs98rq33 | 3600 |
| www.pp.eligibility-checking-engine.education.gov.uk | TXT Record | _vgntzpzbc57g0edkhua5e1eyqxc70fy | 3600 |
| www.eligibility-checking-engine.education.gov.uk | TXT Record | _vmsun05azaunhabqkjtx0hr14wyd1ls | 3600 |

Once these DNS entries are created DWP should be notified, whereupon they will verify the domain ownership and proceed with issuing the certificate.

## After receiving PEMs back from DWP

Place each signed PEM bundle in its environment folder alongside the original private key. The PEM filename should be domain with dots replaced by underscores, for example:

```
DEV/
├── www.dev.eligibility-checking-engine.education.gov.uk.key
└── www_dev_eligibility-checking-engine_education_gov_uk.pem
```

Run this script from the certificates directory:

```powershell
.\Generate-SignedClientAuthPfx.ps1
```

Each PFX is created next to its key, includes the certificates in the PEM bundle, and is checked before being saved. Existing PFX files are never overwritten.

To process selected environments or use a different OpenSSL installation:

```powershell
.\Generate-SignedClientAuthPfx.ps1 -Environment DEV,TEST
.\Generate-SignedClientAuthPfx.ps1 -OpenSSLPath "C:\OpenSSL-Win64\bin\openssl.exe"
```

Use `-CertificatePath` if the environment folders are elsewhere. The script stops on the first error; any PFX files already created are retained. The PFX contains the private key: restrict access, do not commit or email it, and handle it as carefully as the original `.key` file.

Add `-Environment DEV`, for example, to export just one environment.

**Security:** Both files contain the private key without meaningful password protection. Base64 is encoding, not encryption. Restrict access to both files and the secret value, never commit or email them, and remove temporary copies when no longer needed.

## Importing the new certificates

The following steps are conducted in the Azure Portal for each environment.

1) Navigate to the relevant infra key vault, e.g. `ece-dev-kv-infra`, `ece-test-kv-infra`, `ece-pp-kv-infra`, `ece-kv-infra`.
2) Add a new version of the 'dwp-capi-client-certificate' certificate using the corresponding .pfx file.

3) Navigate to the relevant API key vault for each environment, e.g. `ece-dev-kv-ece`, `ece-test-kv-ece`, `ece-pp-kv-ece`, `ece-kv-ece`
4) Add a new value to the 'Dwp--ApiCertificate' secret with the content of the corresponding .pfx.base64 file. 
5) Set the expiry date of the new 'Dwp--ApiCertificate' secret to match the certificate's expiry date.

## Restarting app services to pick up the new secret

The new secret 'Dwp--ApiCertificate' will not be picked up by the API app service until it is restarted. 

## Cleaning up

Remove any temporary or intermediate files, such as the .pfx and .pfx.base64 files, once they are no longer needed to minimize the risk of exposing the private key. Ensure that any copies of the private key and certificates are securely deleted.

A request should be raised to remove the TXT records from DNS using the same External DNS - AWS Route53 service request:
https://dfe.service-now.com/mydfe?id=sc_cat_item&sys_id=898db1201bacaed0593ddce7b04bcb42&referrer=recent_items

For removing the TXT records from DNS, provide the same details as when they were created, specifying the domain and record type, and submit the request through the External DNS - AWS Route53 service request link provided above.
