﻿#region Copyright
/*
 * Software: TickZoom Trading Platform
 * Copyright 2009 M. Wayne Walter
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 * 
 * Business use restricted to 30 days except as otherwise stated in
 * in your Service Level Agreement (SLA).
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <http://www.tickzoom.org/wiki/Licenses>
 * or write to Free Software Foundation, Inc., 51 Franklin Street,
 * Fifth Floor, Boston, MA  02110-1301, USA.
 * 
 */
#endregion

using System;
using System.Collections.Generic;
using System.Threading;

using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Interceptors
{
	public class FillSimulatorPhysical : PhysicalFillSimulator
	{
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(FillSimulatorPhysical));
		private static readonly bool trace = log.IsTraceEnabled;
		private static readonly bool debug = log.IsDebugEnabled;
		private static readonly bool notice = log.IsNoticeEnabled;

		private Dictionary<long,PhysicalOrder> orderMap = new Dictionary<long, PhysicalOrder>();
		private ActiveList<PhysicalOrder> increaseOrders = new ActiveList<PhysicalOrder>();
		private ActiveList<PhysicalOrder> decreaseOrders = new ActiveList<PhysicalOrder>();
		private ActiveList<PhysicalOrder> marketOrders = new ActiveList<PhysicalOrder>();
		private NodePool<PhysicalOrder> nodePool = new NodePool<PhysicalOrder>();

		private List<PhysicalOrder> filledOrders = new List<PhysicalOrder>();
		private Action<LogicalFillBinary> createLogicalFill;
		private Func<int,LogicalOrder> lookupLogicalOrder;
		private bool useSyntheticMarkets = true;
		private bool useSyntheticStops = true;
		private bool useSyntheticLimits = true;
		private SymbolInfo symbol;
		private double actualPosition = 0D;
		private bool isChanged = false;
		
		public FillSimulatorPhysical(SymbolInfo symbol)
		{
			this.symbol = symbol;
		}
	
		public FillSimulatorPhysical(Func<double> getActualPosition, StrategyInterface strategyInterface)
		{
			Strategy strategy = (Strategy) strategyInterface;
		}
		
		private long nextOrderId = 1000;
		public void OnChangeBrokerOrder(PhysicalOrder order)
		{
			if( debug) log.Debug("OnChangeBrokerOrder( " + order + ")");
			CancelBrokerOrder( order);
			CreateBrokerOrder( order);
		}
		
		private void CancelBrokerOrder(PhysicalOrder newOrder) {
			IsChanged = true;
			var oldOrderId = (long) newOrder.BrokerOrder;
			PhysicalOrder oldOrder;
			if( !orderMap.TryGetValue(oldOrderId, out oldOrder)) {
				throw new ApplicationException("Order id " + oldOrderId + " was not found to change order: " + oldOrder);
			}
			RemoveActive( oldOrder);
			orderMap.Remove( oldOrderId);
		}
		
		private void CreateBrokerOrder(PhysicalOrder order) {
			isChanged = true;
			var orderId = Interlocked.Increment(ref nextOrderId);
			order.BrokerOrder = orderId;
			orderMap.Add(orderId,order);
			SortAdjust(order);
		}
		
		public void OnCreateBrokerOrder(PhysicalOrder order)
		{
			if( debug) log.Debug("OnCreateBrokerOrder( " + order + ")");
			CreateBrokerOrder(order);
		}
		
		public void OnCancelBrokerOrder(PhysicalOrder order)
		{
			if( debug) log.Debug("OnCancelBrokerOrder( " + order + ")");
			CancelBrokerOrder(order);
		}
		
		public bool ProcessOrders(Tick tick)
		{
			bool retVal = false;
			if( symbol == null) {
				throw new ApplicationException("Please set the Symbol property for the " + GetType().Name + ".");
			}
			filledOrders.Clear();
			foreach( var order in increaseOrders) {
				OnProcessOrder(order, tick);
			}
			foreach( var order in decreaseOrders) {
				OnProcessOrder(order, tick);
			}
			foreach( var order in marketOrders) {
				OnProcessOrder(order, tick);
			}
			foreach( var order in filledOrders) {
				CancelBrokerOrder( order);
			}
			return retVal;
		}
		
		private void SortAdjust(PhysicalOrder order) {
			switch( order.Type) {
				case OrderType.BuyLimit:					
				case OrderType.SellStop:
					SortAdjust( decreaseOrders, order, (x,y) => y.Price - x.Price);
					break;
				case OrderType.SellLimit:
				case OrderType.BuyStop:
					SortAdjust( increaseOrders, order, (x,y) => x.Price - y.Price);
					break;
				case OrderType.BuyMarket:
				case OrderType.SellMarket:
					Adjust( marketOrders, order);
					break;
				default:
					throw new ApplicationException("Unexpected order type: " + order.Type);
			}
		}
		
		private void RemoveActive(PhysicalOrder order) {
			Remove( increaseOrders, order);
			Remove( decreaseOrders, order);
			Remove( marketOrders, order);
		}
		private void Adjust(ActiveList<PhysicalOrder> list, PhysicalOrder order) {
			if( !list.Contains(order)) {
				var node = nodePool.Create(order);
				list.AddLast(node);
			}
		}
		
		private void Remove(ActiveList<PhysicalOrder> list, PhysicalOrder order) {
			var node = list.Find(order);
			if( node != null) {
				list.Remove(node);
				nodePool.Free(node);
			}
		}
		
		private void SortAdjust(ActiveList<PhysicalOrder> list, PhysicalOrder order, Func<PhysicalOrder,PhysicalOrder,double> compare) {
			if( !list.Contains(order)) {
				var newNode = nodePool.Create(order);
				bool found = false;
				var next = list.First;
				for( var node = next; node != null; node = next) {
					next = node.Next;
					var other = node.Value;
					if( object.ReferenceEquals(order,other)) {
						found = true;
						break;
					} else {
						var result = compare(order,other);
						if( result < 0) {
							list.AddBefore(node,newNode);
							found = true;
							break;
						}
					}
				}
				if( !found) {
					list.AddLast(newNode);
				}
			}
		}
		
	#region ExitOrder
	
		private bool OnProcessOrder(PhysicalOrder order, Tick tick)
		{
			bool retVal = false;
			if (trace) log.Trace("OnProcessOrder()");
	
			switch (order.Type) {
				case OrderType.SellMarket:
					if( ProcessSellMarket(order, tick)) {
						retVal = true;
					}
					break;
				case OrderType.SellStop:
					if( ProcessSellStop(order, tick)) {
						retVal = true;
					}
					break;
				case OrderType.SellLimit:
					if( ProcessSellLimit(order, tick)) {
						retVal = true;
					}
					break;
				case OrderType.BuyMarket:
					if( ProcessBuyMarket(order, tick)) {
						retVal = true;
					}
					break;
				case OrderType.BuyStop:
					if( ProcessBuyStop(order, tick)) {
						retVal = true;
					}
					break;
				case OrderType.BuyLimit:
					if( ProcessBuyLimit(order, tick)) {
						retVal = true;
					}
					break;
			}
			return retVal;
		}
		
		private bool ProcessBuyStop(PhysicalOrder order, Tick tick)
		{
			bool retVal = true;
			double price = tick.IsTrade ? tick.Price : tick.Ask;
			if (price >= order.Price) {
				CreateLogicalFillHelper(order.Size, price, tick.Time, order);
				retVal = true;
			}
			return retVal;
		}

		private bool ProcessSellStop(PhysicalOrder order, Tick tick)
		{
			bool retVal = true;
			var logical = lookupLogicalOrder(order.LogicalOrderId);
			double price;
			switch( logical.TradeDirection) {
				case TradeDirection.Exit:
				case TradeDirection.Reverse:
					price = tick.IsQuote ? tick.Bid : tick.Price;
					break;
				default:
					price = tick.IsQuote ? tick.Ask : tick.Price;
					break;
			}
			if (price <= order.Price) {
				CreateLogicalFillHelper(-order.Size, price, tick.Time, order);
				retVal = true;
			}
			return retVal;
		}

		private bool ProcessBuyMarket(PhysicalOrder order, Tick tick)
		{
			double price = tick.IsQuote ? tick.Ask : tick.Price;
			CreateLogicalFillHelper(order.Size, price, tick.Time, order);
			return true;
		}

		private bool ProcessBuyLimit(PhysicalOrder order, Tick tick)
		{
			double price = tick.IsQuote ? tick.Ask : tick.Price;
			bool isFilled = false;
			if (price <= order.Price) {
				isFilled = true;
			} else if (tick.IsTrade && tick.Price < order.Price) {
				price = order.Price;
				isFilled = true;
			}
			if (isFilled) {
				CreateLogicalFillHelper(order.Size, price, tick.Time, order);
			}
			return isFilled;
		}

		private bool ProcessSellMarket(PhysicalOrder order, Tick tick)
		{
			double price = tick.IsQuote ? tick.Bid : tick.Price;
			CreateLogicalFillHelper(-order.Size, price, tick.Time, order);
			return true;
		}

		private bool ProcessSellLimit(PhysicalOrder order, Tick tick)
		{
			double price = tick.IsQuote ? tick.Bid : tick.Price;
			bool isFilled = false;
			if (price >= order.Price) {
				isFilled = true;
			} else if (tick.IsTrade && tick.Price > order.Price) {
				price = order.Price;
				isFilled = true;
			}
			if (isFilled) {
				CreateLogicalFillHelper(-order.Size, price, tick.Time, order);
			}
			return isFilled;
		}
		
	#endregion
		
		private void CreateLogicalFillHelper(double position, double price, TimeStamp time, PhysicalOrder order) {
			if( debug) log.Debug("Filled: " + order);
			this.actualPosition += position;
			filledOrders.Add(order);
			LogicalFillBinary fill;
			if( order.LogicalOrderId != 0) {
				var logical = lookupLogicalOrder(order.LogicalOrderId);
				fill = new LogicalFillBinary(
					logical.StrategyPosition+position,
					price, time, order.LogicalOrderId);
			} else {
				fill = new LogicalFillBinary(
					actualPosition,
					price, time, order.LogicalOrderId);
			}
			if( debug) log.Debug("Fill price: " + fill);
			createLogicalFill(fill);
		}
		
		public bool UseSyntheticLimits {
			get { return useSyntheticLimits; }
			set { useSyntheticLimits = value; }
		}
		
		public bool UseSyntheticStops {
			get { return useSyntheticStops; }
			set { useSyntheticStops = value; }
		}
		
		public bool UseSyntheticMarkets {
			get { return useSyntheticMarkets; }
			set { useSyntheticMarkets = value; }
		}
		
		public Action<LogicalFillBinary> CreateLogicalFill {
			get { return createLogicalFill; }
			set { createLogicalFill = value; }
		}
		
		public Dictionary<long, PhysicalOrder> PhysicalOrders {
			get { return orderMap; }
		}
		
		public double ActualPosition {
			get { return actualPosition; }
		}
		
		public Func<int, LogicalOrder> LookupLogicalOrder {
			get { return lookupLogicalOrder; }
			set { lookupLogicalOrder = value; }
		}
		
		public bool IsChanged {
			get { return isChanged; }
			set { isChanged = value; }
		}
	}
}
