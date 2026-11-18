# DWP CAPI client authentication certificate update process

## Pre-requisites

- PowerShell 5.1 or later.
- OpenSSL available as openssl on PATH (the scripts also check common Git for Windows/OpenSSL locations); otherwise provide -OpenSSLPath.

## Change management

Refer to central ECE documentation for the official change management process. Changes to the production environment require formal approval and adherence to the established change management procedures.

## Generating CSRs to send to DWP

Run this script from the certificates directory:
```powershell
.\Generate-ClientAuthCSRs.ps1
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

DWP will provide a list of TXT record values for each domain that need to be added to allow the CA to perform validation.  This is actioned using the External DNS - AWS Route53 service request:
https://dfe.service-now.com/mydfe?id=sc_cat_item&sys_id=898db1201bacaed0593ddce7b04bcb42&referrer=recent_items

An example of the request content is shown below:

| Domain | Record Type | Value | TTL |
|--------|-------------|------|-------|-----|
| www.dev.eligibility-checking-engine.education.gov.uk | TXT Record | <insert DEV value here> | 3600 |
| www.test.eligibility-checking-engine.education.gov.uk | TXT Record | <insert TEST value here> | 3600 |
| www.pp.eligibility-checking-engine.education.gov.uk | TXT Record | <insert PP value here> | 3600 |
| www.eligibility-checking-engine.education.gov.uk | TXT Record | <insert PROD value here> | 3600 |

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

The script will generate the signed client authentication PFX file (.pfx) and a base64-encoded version of the PFX (pfx.base64) from the PEM bundles in each environment folder. Each PFX includes the certificates in the PEM bundle, and is checked before being saved. Existing PFX files are never overwritten.

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

The API service in each environment needs to be restarted to pick up the new 'Dwp--ApiCertificate' secret. Navigate to the relevant app service in the Azure Portal and perform a restart.

| Environment | App Service |
|-------------|-------------|
| DEV         | `ece-dev-as-eligibility-checking-engine` |
| TEST        | `ece-test-as-eligibility-checking-engine` |
| PP          | `ece-pp-as-eligibility-checking-engine` |
| PROD        | `ece-as-eligibility-checking-engine` |

## Confirming the new certificate is working

### Submitting a single check using CAPI
- Use Postman in each environment to POST a single FSM check using 'real' data (i.e. do not use TESTER as a surname or any of the test NI values).
- Get the result of the check.
- Any check status other than 'error' indicates a successful outcome.

### Reviewing App Insights logs

Additionally you can search the app insights logs in each environment to verify that requests using the new certificate are being successfully authenticated and to check for any errors related to certificate validation.  Use the search term:
`Exception occurred while calling CAPI citizen match endpoint`
If no exact matches for this error are found, it indicates that the client certificate is being used successfully and no certificate validation errors have occurred.

## Cleaning up

Remove any temporary or intermediate files, such as the .pfx and .pfx.base64 files, once they are no longer needed to minimize the risk of exposing the private key. Ensure that any copies of the private key and certificates are securely deleted.

A request should be raised to remove the TXT records from DNS using the same External DNS - AWS Route53 service request:
https://dfe.service-now.com/mydfe?id=sc_cat_item&sys_id=898db1201bacaed0593ddce7b04bcb42&referrer=recent_items

For removing the TXT records from DNS, provide the same details as when they were created, specifying the domain and record type, and submit the request through the External DNS - AWS Route53 service request link provided above.
