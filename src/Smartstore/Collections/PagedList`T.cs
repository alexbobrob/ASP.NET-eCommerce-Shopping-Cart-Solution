﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using Dasync.Collections;

namespace Smartstore.Collections
{
	public class PagedList<T> : IPagedList<T>
	{
		private bool _queryIsPagedAlready;
		private int? _totalCount;

		private List<T> _list;

		/// <param name="pageIndex">The 0-based page index</param>
		public PagedList(IEnumerable<T> source, int pageIndex, int pageSize)
		{
			Guard.NotNull(source, "source");

			Init(source.AsQueryable(), pageIndex, pageSize, null);
		}

		/// <param name="pageIndex">The 0-based page index</param>
		public PagedList(IEnumerable<T> source, int pageIndex, int pageSize, int totalCount)
		{
			Guard.NotNull(source, "source");

			Init(source.AsQueryable(), pageIndex, pageSize, totalCount);
		}

		private void Init(IQueryable<T> source, int pageIndex, int pageSize, int? totalCount)
		{
			Guard.NotNull(source, nameof(source));
			Guard.PagingArgsValid(pageIndex, pageSize, "pageIndex", "pageSize");

			SourceQuery = source;
			PageIndex = pageIndex;
			PageSize = pageSize;

			_totalCount = totalCount;
			_queryIsPagedAlready = totalCount.HasValue;
		}

		private void EnsureIsLoaded()
		{
			if (_list == null)
			{
				if (_totalCount == null)
				{
					_totalCount = SourceQuery.Count();
				}

				if (_queryIsPagedAlready)
				{
					_list = SourceQuery.ToList();
				}
				else
				{
					_list = ApplyPaging(SourceQuery).ToList();
				}
			}
		}

		private async Task EnsureIsLoadedAsync(CancellationToken cancellationToken = default)
		{
			if (_list == null)
			{
				if (_totalCount == null)
				{
					_totalCount = await SourceQuery.CountAsync(cancellationToken);
				}

				if (_queryIsPagedAlready)
				{
					_list = await SourceQuery.ToListAsync(cancellationToken);
				}
				else
				{
					_list = await ApplyPaging(SourceQuery).ToListAsync(cancellationToken);
				}
			}
		}

		#region IPageable Members

		public IQueryable<T> SourceQuery { get; private set; }

		public IPagedList<T> AlterQuery(Func<IQueryable<T>, IQueryable<T>> alterer)
		{
			var result = alterer?.Invoke(SourceQuery);
			SourceQuery = result ?? throw new InvalidOperationException("The '{0}' delegate must not return NULL.".FormatInvariant(nameof(alterer)));

			return this;
		}

		public IQueryable<T> ApplyPaging(IQueryable<T> query)
		{
			if (PageIndex == 0 && PageSize == int.MaxValue)
			{
				// Paging unnecessary
				return query;
			}
			else
			{
				var skip = PageIndex * PageSize;
				return skip == 0
					? query.Take(PageSize)
					: query.Skip(skip).Take(PageSize);
			}
		}

		public IPagedList<T> Load(bool force = false)
		{
			// Returns instance for chaining.
			if (force && _list != null)
			{
				_list.Clear();
				_list = null;
			}

			EnsureIsLoaded();

			return this;
		}

		public async Task<IPagedList<T>> LoadAsync(bool force = false)
		{
			// Returns instance for chaining.
			if (force && _list != null)
			{
				_list.Clear();
				_list = null;
			}

			await EnsureIsLoadedAsync();

			return this;
		}

		public int PageIndex { get; set; }

		public int PageSize { get; set; }

		public async Task<int> GetTotalCountAsync()
		{
			if (!_totalCount.HasValue)
			{
				_totalCount = await SourceQuery.CountAsync();
			}

			return _totalCount.Value;
		}

		public int TotalCount
		{
			get
			{
				if (!_totalCount.HasValue)
				{
					_totalCount = SourceQuery.Count();
				}

				return _totalCount.Value;
			}
			set
			{
				_totalCount = value;
			}
		}

		public int PageNumber
		{
			get => PageIndex + 1;
			set => PageIndex = value - 1;
		}

		public int TotalPages
		{
			get
			{
				var total = TotalCount / PageSize;

				if (TotalCount % PageSize > 0)
					total++;

				return total;
			}
		}

		public bool HasPreviousPage
		{
			get => PageIndex > 0;
		}

		public bool HasNextPage
		{
			get => (PageIndex < (TotalPages - 1));
		}

		public int FirstItemIndex
		{
			get => (PageIndex * PageSize) + 1;
		}

		public int LastItemIndex
		{
			get => Math.Min(TotalCount, ((PageIndex * PageSize) + PageSize));
		}

		public bool IsFirstPage
		{
			get => (PageIndex <= 0);
		}

		public bool IsLastPage
		{
			get => (PageIndex >= (TotalPages - 1));
		}

		#endregion

		#region IList<T> Members

		public void Add(T item)
		{
			EnsureIsLoaded();
			_list.Add(item);
		}

		public void Clear()
		{
			if (_list != null)
			{
				_list.Clear();
				_list = null;
			}
		}

		public bool Contains(T item)
		{
			EnsureIsLoaded();
			return _list.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			EnsureIsLoaded();
			_list.CopyTo(array, arrayIndex);
		}

		public bool Remove(T item)
		{
			if (_list != null)
			{
				return _list.Remove(item);
			}

			return false;
		}

		public int Count
		{
			get
			{
				EnsureIsLoaded();
				return _list.Count;
			}
		}

		public bool IsReadOnly
		{
			get => false;
		}

		public int IndexOf(T item)
		{
			EnsureIsLoaded();
			return _list.IndexOf(item);
		}

		public void Insert(int index, T item)
		{
			EnsureIsLoaded();
			_list.Insert(index, item);
		}

		public void RemoveAt(int index)
		{
			if (_list != null)
			{
				_list.RemoveAt(index);
			}
		}

		public T this[int index]
		{
			get
			{
				EnsureIsLoaded();
				return _list[index];
			}
			set
			{
				EnsureIsLoaded();
				_list[index] = value;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public IEnumerator<T> GetEnumerator()
		{
			EnsureIsLoaded();
			return _list.GetEnumerator();
		}

		public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
		{
			await EnsureIsLoadedAsync(cancellationToken);

			var e = _list.GetAsyncEnumerator<T>();
			try
			{
				while (await e.MoveNextAsync()) yield return e.Current;
			}
			finally { if (e != null) await e.DisposeAsync(); }
		}

		#endregion

		#region Utils

		public void AddRange(IEnumerable<T> collection)
		{
			EnsureIsLoaded();
			_list.AddRange(collection);
		}

		public ReadOnlyCollection<T> AsReadOnly()
		{
			EnsureIsLoaded();
			return _list.AsReadOnly();
		}

        #endregion
    }
}
