namespace API.Service.ExcelExport
{
    /// <summary>
    /// A write stream (returned by <see cref="IExcelExportFileStore.OpenWrite"/>) that needs an
    /// explicit <see cref="Commit"/> once content has been fully and successfully written.
    /// Backends that write straight to the final location (local disk) do NOT implement this;
    /// staged backends (e.g. FTP) do, so an aborted/failed generation never publishes a partial
    /// file. Disposing without committing discards the staged content.
    /// </summary>
    public interface ICommittableStream
    {
        void Commit();
    }
}
