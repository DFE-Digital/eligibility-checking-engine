# DWP CAPI client authentication certificates

## 🚀 Quick Start

### Run Script

```powershell
\eligibility-checking-engine\certificates\Generate-ClientAuthCSRs.ps1
```

## 📋 Generated Files

### Development Environment
```
DEV/
├── www.dev.eligibility-checking-engine.education.gov.uk.key   ← Private Key (SECURE!)
├── www.dev.eligibility-checking-engine.education.gov.uk.csr   ← Send to CA
└── www.dev.eligibility-checking-engine.education.gov.uk.conf  ← Reference only
```

### Testing Environment
```
TEST/
├── www.test.eligibility-checking-engine.education.gov.uk.key
├── www.test.eligibility-checking-engine.education.gov.uk.csr
└── www.test.eligibility-checking-engine.education.gov.uk.conf
```

### Pre-Production Environment
```
PP/
├── www.pp.eligibility-checking-engine.education.gov.uk.key
├── www.pp.eligibility-checking-engine.education.gov.uk.csr
└── www.pp.eligibility-checking-engine.education.gov.uk.conf
```

### Production Environment
```
PROD/
├── www.eligibility-checking-engine.education.gov.uk.key
├── www.eligibility-checking-engine.education.gov.uk.csr
└── www.eligibility-checking-engine.education.gov.uk.conf
```

## ✅ Certificate Properties

Each CSR has:
- **Key Size**: 2048-bit RSA ✓
- **Signature**: SHA256 with RSA ✓
- **Extended Key Usage**: Client Authentication Only ✓

## 🔐 Security Checklist

- [ ] `.key` files are NOT committed to git (checked by .gitignore)
- [ ] `.key` files are NOT shared via email
- [ ] `.key` files are NOT visible in public repositories
- [ ] Each environment has its own separate key pair
- [ ] Development keys are kept separate from production keys
- [ ] File permissions on `.key` files are restricted to authorized users

## 📝 After Receiving Certificates from CA

For each environment:

1. Save the signed certificate (e.g., `*.crt` or `*.cer`)
2. Place it in the same folder as the `.key` file
3. Example result:
   ```
   DEV/
   ├── www.dev.eligibility-checking-engine.education.gov.uk.key    (Private Key)
   ├── www.dev.eligibility-checking-engine.education.gov.uk.csr    (CSR - keep for reference)
   └── www.dev.eligibility-checking-engine.education.gov.uk.crt    (New - Signed Certificate)
   ```

