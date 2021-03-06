﻿using System;
using System.Collections.Generic;
using System.Linq;
using Merchello.Core.Models;
using Merchello.Core.Models.EntityBase;
using Merchello.Core.Models.Rdbms;
using Merchello.Core.Persistence.Factories;
using Merchello.Core.Persistence.Querying;
using Merchello.Core.Persistence.UnitOfWork;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Querying;

namespace Merchello.Core.Persistence.Repositories
{
    internal class LineItemRepository<TDto> : MerchelloPetaPocoRepositoryBase<ILineItem>, ILineItemRepository
        where TDto : ILineItemDto
    {

        public LineItemRepository(IDatabaseUnitOfWork work, IRuntimeCacheProvider cache)
            : base(work, cache)
        { }

        #region Overrides ILineItemRepository
        

        protected override ILineItem PerformGet(Guid key)
        {
            var sql = GetBaseQuery(false)
               .Where(GetBaseWhereClause(), new { Key = key });

            var dto = (ILineItemDto)Database.Fetch<TDto>(sql).FirstOrDefault();

            if (dto == null)
                return null;


            var lineItem = GetEntity(dto);

            return lineItem;
        }

        protected override IEnumerable<ILineItem> PerformGetAll(params Guid[] keys)
        {
            if (keys.Any())
            {
                foreach (var key in keys)
                {
                    yield return Get(key);
                }
            }
            else
            {
                var dtos = Database.Fetch<TDto>(GetBaseQuery(false));
                foreach (var dto in dtos)
                {
                    yield return GetEntity(dto);
                }
            }
        }

   
        protected override IEnumerable<ILineItem> PerformGetByQuery(IQuery<ILineItem> query)
        {
           // convert the IQuery
            var q = query as Querying.Query<ILineItem>;
            if (typeof (TDto) == typeof (InvoiceItemDto))
            {
                var converted = new Querying.Query<IInvoiceLineItem>();
                foreach (var item in q.WhereClauses())
                {
                    converted.WhereClauses().Add(item);
                }
                return PerformGetByQuery(converted);
            }

            if (typeof (TDto) == typeof (OrderItemDto))
            {
                var converted = new Querying.Query<IOrderLineItem>();
                foreach (var item in q.WhereClauses())
                {
                    converted.WhereClauses().Add(item);
                }
                return PerformGetByQuery(converted);
            }

            var final = new Querying.Query<IItemCacheLineItem>();
            foreach (var item in q.WhereClauses())
            {
                final.WhereClauses().Add(item);
            }
            return PerformGetByQuery(final);
        }

        protected IEnumerable<IInvoiceLineItem> PerformGetByQuery(IQuery<IInvoiceLineItem> query)
        {
            var sqlClause = GetBaseQuery(false);

            var translator = new SqlTranslator<IInvoiceLineItem>(sqlClause, query);
            var sql = translator.Translate();

            var dtos = Database.Fetch<InvoiceItemDto>(sql);

            return dtos.DistinctBy(x => x.Key).Select(dto => (IInvoiceLineItem)Get(dto.Key));
        }

        protected IEnumerable<IOrderLineItem> PerformGetByQuery(IQuery<IOrderLineItem> query)
        {
            var sqlClause = GetBaseQuery(false);

            var translator = new SqlTranslator<IOrderLineItem>(sqlClause, query);
            var sql = translator.Translate();

            var dtos = Database.Fetch<OrderItemDto>(sql);

            return dtos.DistinctBy(x => x.Key).Select(dto => (IOrderLineItem)Get(dto.Key));
        }

        protected IEnumerable<IItemCacheLineItem> PerformGetByQuery(IQuery<IItemCacheLineItem> query)
        {
            var sqlClause = GetBaseQuery(false);

            var translator = new SqlTranslator<IItemCacheLineItem>(sqlClause, query);
            var sql = translator.Translate();

            var dtos = Database.Fetch<InvoiceItemDto>(sql);

            return dtos.DistinctBy(x => x.Key).Select(dto => (IItemCacheLineItem)Get(dto.Key));
        }
        
        protected override Sql GetBaseQuery(bool isCount)
        {
            var sql = new Sql();
            sql.Select(isCount ? "COUNT(*)" : "*")
               .From<TDto>();

            return sql;
        }


        private ILineItem GetEntity(ILineItemDto dto)
        {
            var factory = new LineItemFactory();

            if (typeof(TDto) == typeof(InvoiceItemDto)) return factory.BuildEntity((InvoiceItemDto)dto);
            if (typeof(TDto) == typeof(OrderItemDto)) return factory.BuildEntity((OrderItemDto)dto);
            return factory.BuildEntity((ItemCacheItemDto)dto);
        }

        private ILineItemDto GetDto(ILineItem entity)
        {
            var factory = new LineItemFactory();

            if (typeof(TDto) == typeof(InvoiceItemDto)) return factory.BuildDto((IInvoiceLineItem)entity);
            if (typeof(TDto) == typeof(OrderItemDto)) return factory.BuildDto((IOrderLineItem)entity);
            return factory.BuildDto((IItemCacheLineItem)entity);
        }

        private static string GetMerchTableName()
        {
            return typeof (TDto) == typeof (InvoiceItemDto) ? "merchInvoiceItem"
                : typeof (TDto) == typeof (OrderItemDto) ? "merchOrderItem" 
                : "merchItemCacheItem";
        }

        protected override string GetBaseWhereClause()
        {
            return GetMerchTableName() + ".pk = @Key";
        }

        protected override IEnumerable<string> GetDeleteClauses()
        {
            return new List<string>()
            {
                "DELETE FROM " + GetMerchTableName() + " WHERE pk = @Key"
            };
        }

        protected override void PersistNewItem(ILineItem entity)
        {
            ((Entity)entity).AddingEntity();
           
            var dto = GetDto(entity);
            Database.Insert(dto);
            entity.Key = dto.Key;
            entity.ResetDirtyProperties();
        }

        protected override void PersistUpdatedItem(ILineItem entity)
        {
            ((Entity)entity).UpdatingEntity();

            var dto = GetDto(entity);

            Database.Update(dto);
            entity.ResetDirtyProperties();
        }

        #endregion

        public IEnumerable<ILineItem> GetByContainerKey(Guid containerKey)
        {
            
            if (typeof(TDto) == typeof(InvoiceItemDto))
            {

                var query = Querying.Query<IInvoiceLineItem>.Builder.Where(x => x.ContainerKey == containerKey);
                return PerformGetByQuery(query);
            }

            if (typeof(TDto) == typeof(OrderItemDto))
            {
                var query = Querying.Query<IOrderLineItem>.Builder.Where(x => x.ContainerKey == containerKey);
                return PerformGetByQuery(query);
            }

            var itemCacheItemQuery = Querying.Query<IItemCacheLineItem>.Builder.Where(x => x.ContainerKey == containerKey);                      
            return PerformGetByQuery(itemCacheItemQuery);
            
        }

        public void SaveLineItem(IEnumerable<ILineItem> items, Guid containerKey)
        {
            var lineItems = items as ILineItem[] ?? items.ToArray();

            var existing = GetByContainerKey(containerKey);

            // assert there are no existing items not in the new set of items.  If there are ... delete them
            var toDelete = existing.Where(x => !items.Any(item => item.Key == x.Key)).ToArray();
            if (toDelete.Any())
            {
                foreach (var d in toDelete)
                {
                    var dto = GetDto(d);
                    Database.Delete(dto);
                }
            }

            foreach (var item in lineItems)
            {
                // In the mapping between different line item types the container key is 
                // invalidated so we need to set it to the current container.
                if (!item.ContainerKey.Equals(containerKey)) item.ContainerKey = containerKey;

                SaveLineItem(item);
            }
        }

        public void SaveLineItem(ILineItem item)
        {          
            if (!item.HasIdentity)
            {
                ((Entity)item).AddingEntity();
                PersistNewItem(item);
            }
            else
            {
                ((Entity)item).UpdatingEntity();
                PersistUpdatedItem(item);
            }            
        }       
    }
}