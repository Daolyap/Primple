# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in Primple, please report it by emailing:

**contact@daolyap.dev**

Please include:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

**Do not** open a public GitHub issue for security vulnerabilities.

We will acknowledge receipt within 48 hours and provide a detailed response within 7 days.

## Security Features

### Current Implementation

1. **Exception Handling**
   - Fatal errors are logged to secure location (`%LocalAppData%\Primple\Logs`)
   - User-facing error messages do not expose sensitive implementation details
   - Stack traces and detailed errors are never shown to end users

2. **UAC Configuration**
   - Application runs with `asInvoker` privileges (no elevation required)
   - Explicit UAC manifest prevents privilege escalation vulnerabilities
   - Compatible with Windows 10 and Windows 11

3. **Code Quality**
   - Nullable reference types enabled to prevent null reference exceptions
   - All compiler warnings treated as errors
   - Latest Roslyn analyzers enabled for security best practices
   - Comprehensive code analysis (`latest-all`)

4. **Dependency Management**
   - All dependencies are from official Microsoft sources
   - Regular dependency updates
   - No known CVEs in current dependency versions

### Planned Security Enhancements

1. **Code Signing**
   - Authenticode signing with trusted certificate
   - Timestamping for long-term verification
   - SmartScreen reputation building

2. **Update Security**
   - Secure update mechanism with signature verification
   - HTTPS-only update checks
   - Rollback capability for failed updates

3. **Data Protection**
   - DPAPI for local sensitive data encryption
   - Secure credential storage
   - Memory protection for sensitive operations

## Security Best Practices for Contributors

### Code Contributions

1. **Input Validation**
   - Always validate user input
   - Use allow-lists rather than deny-lists
   - Sanitize file paths before file operations

2. **Error Handling**
   - Never expose internal paths or implementation details in error messages
   - Log detailed errors securely
   - Show user-friendly messages to end users

3. **Cryptography**
   - Use only strong algorithms (AES-256, RSA-2048+, SHA-256+)
   - Never implement custom cryptography
   - Use .NET's built-in cryptographic APIs

4. **File Operations**
   - Validate all file paths
   - Check for path traversal attacks
   - Use `Path.GetFullPath()` and verify paths are within expected directories

5. **Network Operations** (when implemented)
   - Use HTTPS/TLS 1.2+ only
   - Validate all SSL/TLS certificates
   - Never disable certificate validation

6. **Dependency Security**
   - Only use well-maintained, reputable packages
   - Review dependencies for known vulnerabilities
   - Keep dependencies up to date

### Testing

1. **Security Testing**
   - Write tests for security-critical code
   - Test error handling paths
   - Verify input validation

2. **Fuzzing**
   - Consider fuzzing file parsers and input handlers
   - Test with malformed/malicious input

## Security Contacts

- **Primary Contact**: contact@daolyap.dev
- **Response Time**: Within 48 hours
- **Security Updates**: Via GitHub Releases

## Disclosure Policy

We follow **Coordinated Vulnerability Disclosure**:
1. Report received and acknowledged
2. Vulnerability verified and assessed
3. Fix developed and tested
4. Security advisory published
5. Fix released
6. Public disclosure after fix is available

Typical timeline: 30-90 days from report to public disclosure.

## Security Tools

This project uses:
- **Roslyn Analyzers**: Static code analysis
- **GitHub Dependabot**: Dependency vulnerability scanning (recommended)
- **CodeQL**: Security scanning in CI/CD (recommended)

## Additional Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Microsoft Security Development Lifecycle](https://www.microsoft.com/en-us/securityengineering/sdl)
- [CWE Top 25](https://cwe.mitre.org/top25/)
- [.NET Security Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/security/)
