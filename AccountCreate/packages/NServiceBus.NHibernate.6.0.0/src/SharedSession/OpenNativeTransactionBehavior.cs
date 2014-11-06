namespace NServiceBus.Persistence.NHibernate
{
    using System;
    using System.Transactions;
    using global::NHibernate;
    using Pipeline;
    using Pipeline.Contexts;

    class OpenNativeTransactionBehavior : IBehavior<IncomingContext>
    {
        public IStorageSessionProvider StorageSessionProvider { get; set; }

        public string ConnectionString { get; set; }

        public void Invoke(IncomingContext context, Action next)
        {
            if (Transaction.Current != null)
            {
                next();
                return;
            }

            var lazyTransaction = new Lazy<ITransaction>(() => StorageSessionProvider.Session.BeginTransaction());
            context.Set(string.Format("LazyNHibernateTransaction-{0}", ConnectionString), lazyTransaction);

            try
            {
                next();

                if (lazyTransaction.IsValueCreated)
                {
                    if (lazyTransaction.Value.IsActive)
                    {
                        lazyTransaction.Value.Commit();
                    }
                }
            }
            finally
            {
                context.Remove(string.Format("LazyNHibernateTransaction-{0}", ConnectionString));
            }
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("OpenNHibernateTransaction", typeof(OpenNativeTransactionBehavior), "Makes sure that there is a NHibernate ITransaction wrapping the pipeline")
            {
                InsertAfter(WellKnownStep.ExecuteUnitOfWork);
                InsertBeforeIfExists(WellKnownStep.InvokeSaga);
                InsertBeforeIfExists("OutboxRecorder");
            }
        }
    }
}
