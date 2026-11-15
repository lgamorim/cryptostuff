namespace CryptoStuff.Core;

/// <summary>How a feature service's save of one record ended.</summary>
internal enum SaveOutcome
{
    /// <summary>The change was written.</summary>
    Saved,

    /// <summary>
    /// The save conflicted with stored state, such as a taken name or another
    /// write moving the version on, and the record still exists.
    /// </summary>
    Conflicted,

    /// <summary>The save conflicted because another writer deleted the record since it was read.</summary>
    Deleted,
}
