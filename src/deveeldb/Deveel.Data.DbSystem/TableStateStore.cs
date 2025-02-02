﻿// 
//  Copyright 2010-2015 Deveel
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Deveel.Data.Store;

namespace Deveel.Data.DbSystem {
	class TableStateStore {
		private IArea headerArea;

		private long delAreaPointer;
		private Dictionary<string, TableState> deleteList;
		private bool delListChange;

		private long visAreaPointer;
		private Dictionary<string, TableState> visibleList;
		private bool visListChange;

		private int currentTableId;

		private const int Magic = 0x0BAC8001;

		public TableStateStore(IStore store) {
			if (store == null)
				throw new ArgumentNullException("store");

			Store = store;
		}

		public IStore Store { get; private set; }

		private void ReadStateResourceList(Dictionary<string, TableState> list, long pointer) {
			using (var reader = new BinaryReader(Store.GetAreaInputStream(pointer), Encoding.Unicode)) {
				reader.ReadInt32(); // version

				int count = (int) reader.ReadInt64();
				for (int i = 0; i < count; ++i) {
					long tableId = reader.ReadInt64();
					string name = reader.ReadString();

					list.Add(name, new TableState((int)tableId, name));
				}
			}
		}

		private static byte[] SerializeResources(IEnumerable<TableState> list) {
			using (var stream = new MemoryStream()) {
				using (var writer = new BinaryWriter(stream, Encoding.Unicode)) {
					writer.Write(1); // version
					int sz = list.Count();
					writer.Write((long) sz);
					foreach (var state in list) {
						writer.Write((long)state.TableId);
						writer.Write(state.SourceName);
					}

					writer.Flush();

					return stream.ToArray();
				}
			}
		}

		private long WriteListToStore(IEnumerable<TableState> list) {
			var bytes = SerializeResources(list);

			var area = Store.CreateArea(bytes.Length);
			long listP = area.Id;
			area.Write(bytes, 0, bytes.Length);
			area.Flush();

			return listP;
		}

		public long Create() {
			lock (this) {
				// Allocate empty visible and deleted tables area
				var visTablesArea = Store.CreateArea(12);
				var delTablesArea = Store.CreateArea(12);
				visAreaPointer = visTablesArea.Id;
				delAreaPointer = delTablesArea.Id;

				// Write empty entries for both of these
				visTablesArea.WriteInt4(1);
				visTablesArea.WriteInt8(0);
				visTablesArea.Flush();
				delTablesArea.WriteInt4(1);
				delTablesArea.WriteInt8(0);
				delTablesArea.Flush();

				// Now allocate an empty state header
				var headerWriter = Store.CreateArea(32);
				long headerP = headerWriter.Id;
				headerWriter.WriteInt4(Magic);
				headerWriter.WriteInt4(0);
				headerWriter.WriteInt8(0);
				headerWriter.WriteInt8(visAreaPointer);
				headerWriter.WriteInt8(delAreaPointer);
				headerWriter.Flush();

				headerArea = Store.GetArea(headerP, false);

				// Reset currentTableId
				currentTableId = 0;

				visibleList = new Dictionary<string, TableState>();
				deleteList = new Dictionary<string, TableState>();

				// Return pointer to the header area
				return headerP;
			}
		}

		public void Open(long offset) {
			lock (this) {
				headerArea = Store.GetArea(offset);
				int magicValue = headerArea.ReadInt4();
				if (magicValue != Magic)
					throw new IOException("Magic value for state header area is incorrect.");

				if (headerArea.ReadInt4() != 0)
					throw new IOException("Unknown version for state header area.");

				currentTableId = (int)headerArea.ReadInt8();
				visAreaPointer = headerArea.ReadInt8();
				delAreaPointer = headerArea.ReadInt8();

				// Setup the visible and delete list
				visibleList = new Dictionary<string, TableState>();
				deleteList = new Dictionary<string, TableState>();

				// Read the resource list for the visible and delete list.
				ReadStateResourceList(visibleList, visAreaPointer);
				ReadStateResourceList(deleteList, delAreaPointer);
			}
		}

		public int NextTableId() {
			lock (this) {
				int curCounter = currentTableId;
				++currentTableId;

				try {
					Store.LockForWrite();

					// Update the state in the file
					headerArea.Position = 8;
					headerArea.WriteInt8(currentTableId);

					// Check out the change
					headerArea.Flush();
				} finally {
					Store.UnlockForWrite();
				}

				return curCounter;
			}
		}

		public IEnumerable<TableState> GetVisibleList() {
			lock (this) {
				return visibleList.Values.AsEnumerable();
			}
		}

		public IEnumerable<TableState> GetDeleteList() {
			lock (this) {
				return deleteList.Values.AsEnumerable();
			}
		}

		public bool ContainsVisibleResource(int tableId) {
			lock (this) {
				foreach (var resource in visibleList.Values) {
					if (resource.TableId == tableId)
						return true;
				}
				return false;
			}
		}

		public void AddVisibleResource(TableState resource) {
			lock (this) {
				visibleList.Add(resource.SourceName, resource);
				visListChange = true;
			}
		}

		public void AddDeleteResource(TableState resource) {
			lock (this) {
				deleteList.Add(resource.SourceName, resource);
				delListChange = true;
			}
		}

		public void RemoveVisibleResource(string name) {
			lock (this) {
				if (visibleList.Remove(name))
					visListChange = true;
			}
		}

		public void RemoveDeleteResource(string name) {
			lock (this) {
				if (deleteList.Remove(name))
					delListChange = true;
			}
		}

		public void Flush() {
			lock (this) {
				bool changes = false;
				long newVisP = visAreaPointer;
				long newDelP = delAreaPointer;

				try {
					Store.LockForWrite();

					// If the lists changed, then Write new state areas to the store.
					if (visListChange) {
						newVisP = WriteListToStore(visibleList.Values);
						visListChange = false;
						changes = true;
					}
					if (delListChange) {
						newDelP = WriteListToStore(deleteList.Values);
						delListChange = false;
						changes = true;
					}

					// Commit the changes,
					if (changes) {
						headerArea.Position = 16;
						headerArea.WriteInt8(newVisP);
						headerArea.WriteInt8(newDelP);
						headerArea.Flush();

						if (visAreaPointer != newVisP) {
							Store.DeleteArea(visAreaPointer);
							visAreaPointer = newVisP;
						}

						if (delAreaPointer != newDelP) {
							Store.DeleteArea(delAreaPointer);
							delAreaPointer = newDelP;
						}
					}
				} finally {
					Store.UnlockForWrite();
				}
			}
		}

		#region TableState

		public class TableState {
			public TableState(int tableId, string sourceName) {
				TableId = tableId;
				SourceName = sourceName;
			}

			public int TableId { get; private set; }

			public string SourceName { get; private set; }
		}

		#endregion
	}
}
