using System.Runtime.InteropServices;
using System.Text;

namespace GitTab.App.Services;

public interface ICredentialStore
{
    /// <summary>Read the (username, secret) stored for <paramref name="target"/>, or null.</summary>
    (string User, string Secret)? Get(string target);

    /// <summary>Store (or overwrite) credentials for <paramref name="target"/>.</summary>
    void Save(string target, string user, string secret);

    /// <summary>Remove stored credentials for <paramref name="target"/> (no-op if absent).</summary>
    void Delete(string target);

    /// <summary>List the target names of every credential Git Tab has stored (see <see cref="CredentialKey.Prefix"/>).</summary>
    IReadOnlyList<string> List();
}

/// <summary>
/// Stores git credentials in the Windows Credential Manager — DPAPI-encrypted per user, the same
/// vault the official Git Credential Manager uses. A Personal Access Token entered once in the GUI
/// is reused for every push/pull on that host, so Git Tab needs no console credential setup.
/// </summary>
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;

    public (string User, string Secret)? Get(string target)
    {
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var ptr) || ptr == IntPtr.Zero)
            return null;
        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            var user = cred.UserName ?? string.Empty;
            var secret = string.Empty;
            if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero)
            {
                var bytes = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
                secret = Encoding.Unicode.GetString(bytes);
            }
            return (user, secret);
        }
        finally
        {
            CredFree(ptr);
        }
    }

    public void Save(string target, string user, string secret)
    {
        var blob = Encoding.Unicode.GetBytes(secret ?? string.Empty);
        var blobPtr = Marshal.AllocHGlobal(blob.Length == 0 ? 1 : blob.Length);
        try
        {
            if (blob.Length > 0) Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                UserName = user,
                CredentialBlob = blobPtr,
                CredentialBlobSize = blob.Length,
                Persist = CRED_PERSIST_LOCAL_MACHINE
            };
            if (!CredWrite(ref cred, 0))
                throw new InvalidOperationException(
                    $"자격 증명을 저장하지 못했습니다. (CredWrite failed, error {Marshal.GetLastWin32Error()})");
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public void Delete(string target) => CredDelete(target, CRED_TYPE_GENERIC, 0);

    public IReadOnlyList<string> List()
    {
        // CredEnumerate fails with ERROR_NOT_FOUND (1168) when nothing matches the filter — that's
        // an empty vault, not an error, so any failure here just means "no stored credentials".
        if (!CredEnumerate($"{CredentialKey.Prefix}*", 0, out var count, out var credentials))
            return Array.Empty<string>();
        try
        {
            var targets = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var credPtr = Marshal.ReadIntPtr(credentials, i * IntPtr.Size);
                var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                if (cred.TargetName is not null) targets.Add(cred.TargetName);
            }
            return targets;
        }
        finally
        {
            CredFree(credentials);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredReadW")]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredWriteW")]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW")]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredEnumerateW")]
    private static extern bool CredEnumerate(string filter, int flags, out int count, out IntPtr credentials);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr cred);
}
