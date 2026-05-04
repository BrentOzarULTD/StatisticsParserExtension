using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace StatisticsParser.Vsix.Capture
{
    // Public façade for Phase 8b. Acquires the brokered proxy, then pages through
    // GetMessagesTabSegmentAsync until TotalLength chars have been collected. The captured text is
    // a snapshot at command-fire time; if a query is still running and the Messages tab continues
    // to grow, only the prefix that existed when the first segment returned is captured.
    public static class MessagesTabReader
    {
        private const int PageSize = 65536;

        public static async Task<MessagesCaptureResult> GetMessagesTextAsync(
            AsyncPackage package, CancellationToken ct)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            MessagesBrokeredClient client;
            try
            {
                client = await MessagesBrokeredClient.CreateAsync(package, ct).ConfigureAwait(true);
            }
            catch (FileNotFoundException ex)
            {
                return MessagesCaptureResult.ContractsAssemblyMissing(ex);
            }
            catch (InvalidOperationException ex)
            {
                return MessagesCaptureResult.ProxyUnavailable(ex);
            }
            catch (Exception ex)
            {
                return MessagesCaptureResult.Failed(ex);
            }

            try
            {
                if (!await client.IsMessagesPaneAvailableAsync(ct).ConfigureAwait(true))
                    return MessagesCaptureResult.NoActiveWindow();

                var first = await client.GetMessagesSegmentAsync(0, PageSize, ct).ConfigureAwait(true);
                if (first.TotalLength <= 0)
                    return MessagesCaptureResult.EmptyMessages();

                int totalLength = first.TotalLength;
                var sb = new StringBuilder(totalLength);
                sb.Append(first.Content);
                int start = first.Content.Length;
                int maxIterations = (totalLength / PageSize) + 4;
                int iteration = 1;

                while (start < totalLength)
                {
                    if (++iteration > maxIterations)
                        return MessagesCaptureResult.Failed(new InvalidOperationException(
                            "Paging loop exceeded iteration cap (start=" + start +
                            ", totalLength=" + totalLength + ")."));

                    var seg = await client.GetMessagesSegmentAsync(start, PageSize, ct).ConfigureAwait(true);
                    if (string.IsNullOrEmpty(seg.Content))
                        return MessagesCaptureResult.Failed(new InvalidOperationException(
                            "Paging stalled at start=" + start + " of " + totalLength +
                            " (segment returned empty content)."));

                    sb.Append(seg.Content);
                    start += seg.Content.Length;
                }

                return MessagesCaptureResult.Ok(sb.ToString());
            }
            catch (Exception ex)
            {
                return MessagesCaptureResult.Failed(ex);
            }
            finally
            {
                client.Dispose();
            }
        }
    }
}
