﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Threading;

namespace Essential.Net.Mail
{
    // This class is needed because although SmtpClient has a SendAsync() method, it also states:
    // "After calling SendAsync, you must wait for the e-mail transmission to complete before attempting 
    // to send another e-mail message using Send or SendAsync."

    // That means any following message must block until the SmtpClient is available, but of course
    // there isn't really any status flag or anything, so you would need to implement that all
    // yourself (meanwhile blocking the calling thread).

    // So, to avoid blocking the calling thread there are two possible options.
    // One is to spin off a sending thread for each message that blocks until it can proceed;
    // threads are expensive, so the ThreadPool may be an option (to queue work up),
    // but generally you have less control over the ThreadPool (it is shared with the system).
  
    // The other alternative is to have a queue in memory where the calling thread adds to the
    // queue, and then have a background thread (or several) that keeps polling and dequeues messages 
    // when available and sends them. This may give a bit better visibility to the size of the queue
    // (rather than hidden off as blocked threads or ThreadPool queued work items). e.g. if the size
    // of the queue is too large it can start "dropping packets", and simply not send messages when
    // flooded.

    // Note that the blocking threads / ThreadPool option pretty much works the same (i.e. the messages
    // are queued up somewhere in memory), just with less control.

    // Also note that in both cases you might as well send the email message synchronously, as
    // you need to wait for it to complete anyway.
    
    // Another way is use the async thread that the SmtpClient calls back on to check the queue
    // and process the next message if necessary, however while SendAsync() sends the message
    // asynchronously, the initial connection is done synchronously (and may throw an
    // exception). If we don't even want to block on the initial connection, then we need to use
    // a separate worker thread.



    // TODO: Do we really want this public?? -- it is used for testing.

    internal class SmtpWorkerPoolC : IDisposable
    {
        const int maxExitWaitMilliseconds = 2000;
        const int exitCheckIntervalMilliseconds = 100;

        int maxConnections;
        Queue<SmtpWorkerAsyncResult> messageQueue = new Queue<SmtpWorkerAsyncResult>();
        object queueLock = new object();

        List<SmtpClient> activeSmtpClients = new List<SmtpClient>();

        public SmtpWorkerPoolC(int maxConnections)
        {
            this.maxConnections = maxConnections;
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
        }

        public IAsyncResult BeginSend(MailMessage message, AsyncCallback callback, object state)
        {
            var asyncResult = new SmtpWorkerAsyncResult(message, callback, state);
            SmtpClient newWorker = null;
            lock (queueLock)
            {
                // lock keeps queue count and active client count in sync
                messageQueue.Enqueue(asyncResult);
                newWorker = CheckNewWorker();
            }
            if (newWorker != null)
            {
                ProcessQueue(newWorker);
            }
            return asyncResult;
        }

        public IAsyncResult BeginSend(string from, string recipients, string subject, string body, AsyncCallback callback, object state)
        {
            var mailMessage = new MailMessage(from, recipients, subject, body);
            var asyncResult = BeginSend(mailMessage,
                    (ar) => {
                        mailMessage.Dispose();
                        if (callback != null)
                        {
                            callback(ar);
                        }
                    },
                    state);
            return asyncResult;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Want API to be instance method.")]
        public void EndSend(IAsyncResult asyncResult)
        {
            var smtpResult = asyncResult as SmtpWorkerAsyncResult;
            if (smtpResult == null)
            {
                throw new ArgumentException("IAsyncResult is of the wrong type.", "asyncResult");
            }
            SmtpWorkerAsyncResult.End(smtpResult);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var client in activeSmtpClients)
                {
                    TryDisposeSmtpClient(client);
                }
                while (messageQueue.Count > 0)
                {
                    var asyncResult = messageQueue.Dequeue();
                    asyncResult.Dispose();
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Portability", "CA1903:UseOnlyApiFromTargetedFramework", MessageId = "System.DateTimeOffset", Justification = "Deliberate dependency, .NET 2.0 SP1 required.")]
        private SmtpClient CheckNewWorker()
        {
            SmtpClient newWorker = null;
            if (activeSmtpClients.Count < maxConnections)
            {
                newWorker = new SmtpClient();
                activeSmtpClients.Add(newWorker);
                newWorker.SendCompleted += SendCompleted;
                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:mm':'ss.ffffff}: CheckNewWorker, active count = {1}, queue length = {2}", DateTimeOffset.Now, activeSmtpClients.Count, messageQueue.Count));
            }
            return newWorker;
        }

        private void SendCompleted(object sender, AsyncCompletedEventArgs e)
        {
            SmtpWorkerAsyncResult completedAsyncResult = (SmtpWorkerAsyncResult)e.UserState;
            if (e.Error != null)
            {
                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "SendCompleted Exception: {0}", e.Error));
            }
            completedAsyncResult.Complete(e.Error, false);
            completedAsyncResult.Dispose();
            // Use the completed thread to check for any new messages in queue
            var smtpClient = (SmtpClient)sender;
            ProcessQueue(smtpClient);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is reported in IAsyncResult for client to handle.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Portability", "CA1903:UseOnlyApiFromTargetedFramework", MessageId = "System.DateTimeOffset", Justification = "Deliberate dependency, .NET 2.0 SP1 required.")]
        private void ProcessQueue(SmtpClient clientToUse)
        {
            SmtpWorkerAsyncResult asyncResultToProcess = null;
            lock (queueLock)
            {
                // lock keeps queue count and active client count in sync
                if (messageQueue.Count > 0)
                {
                    asyncResultToProcess = messageQueue.Dequeue();
                    Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:mm':'ss.ffffff}: ProcessQueue, active count = {1}, queue length = {2}", DateTimeOffset.Now, activeSmtpClients.Count, messageQueue.Count));
                }
                else
                {
                    // Nothing to process; shut down the SmtpClient
                    activeSmtpClients.Remove(clientToUse);
                }
            };
            if (asyncResultToProcess != null)
            {
                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:mm':'ss.ffffff}: Before SendAsync, active count = {1}, queue length = {2}", DateTimeOffset.Now, activeSmtpClients.Count, messageQueue.Count));
                Exception exception = null;
                try
                {
                    clientToUse.SendAsync(asyncResultToProcess.Message, asyncResultToProcess);
                }
                catch (Exception ex)
                {
                    exception = ex;
                    Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "SendAsync Exception: {0}", exception));
                }
                Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:mm':'ss.ffffff}: After SendAsync, active count = {1}, queue length = {2}", DateTimeOffset.Now, activeSmtpClients.Count, messageQueue.Count));
                if (exception != null)
                {
                    asyncResultToProcess.Complete(exception, true);
                }
            }
            else
            {
                TryDisposeSmtpClient(clientToUse);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Portability", "CA1903:UseOnlyApiFromTargetedFramework", MessageId = "System.DateTimeOffset", Justification = "Deliberate dependency, .NET 2.0 SP1 required.")]
        private void TryDisposeSmtpClient(SmtpClient smtpClient)
        {
            Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:mm':'ss.ffffff}: TryDisposeSmtpClient, active count = {1}, queue length = {2}", DateTimeOffset.Now, activeSmtpClients.Count, messageQueue.Count));
            if (typeof(SmtpClient).GetInterface("IDisposable") != null)//In .NET 4, SmtpClient has IDisposable.
            {
                IDisposable disposable = smtpClient as IDisposable;
                disposable.Dispose();
            }
        }

        void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            SendAllBeforeExit();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Portability", "CA1903:UseOnlyApiFromTargetedFramework", MessageId = "System.DateTimeOffset", Justification = "Deliberate dependency, .NET 2.0 SP1 required.")]
        void SendAllBeforeExit()
        {
            Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:mm':'ss.ffffff}: SendAllBeforeExit, active count = {1}, queue length = {2}", DateTimeOffset.Now, activeSmtpClients.Count, messageQueue.Count));
            int totalWaitTime = 0;
            while (activeSmtpClients.Count > 0 && totalWaitTime < maxExitWaitMilliseconds)
            {
                Thread.Sleep(exitCheckIntervalMilliseconds);
                totalWaitTime += exitCheckIntervalMilliseconds;
            }
            Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:mm':'ss.ffffff}: DONE SendAllBeforeExit, active count = {1}, queue length = {2}", DateTimeOffset.Now, activeSmtpClients.Count, messageQueue.Count));
        }


    }
}
