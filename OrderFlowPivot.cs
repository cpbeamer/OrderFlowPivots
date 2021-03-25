#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class OrderFlowPivots : Strategy
	{
		// Declaration of all the potential pivots
		private double priorHigh 	= 0;
		private double priorLow		= 0;
		private double priorClose	= 0;
		private double currentOpen 	= 0;
		private double currentLow 	= 0;
		private double currentHigh 	= 0;
		private double openPrice 	= 0;
		private double currentVAL   = 0;
		private double currentVAH   = 0;
		private double currentPOC   = 0;
		private double priorVAL     = 0;
		private double priorVAH     = 0;
		
		// Set up the order, traget, and stop variables
		private Order entry1 = null;
		private Order entry2 = null;
		private Order entry3 = null;
		private Order entry4 = null;
		private Order stop1 = null;
		private Order stop2 = null;
		private Order stop3 = null;
		private Order stop4 = null;
		private Order target1 = null;
		private Order target2 = null;
		private Order target3 = null;
		private Order target4 = null;
		private int sumFilled = 0;
		private int barNumberOfOrder = 0;
		
		// Arrays used throughout the code
		private double[] supports = new double[0];
		private double[] resistances = new double[0];
		private Order[] allOrders;
		private Order[] stopOrders;
		private Order[] targetOrders;
		
		//String names for order objects
		private string[] entryStrings = new String[] { "entry1", "entry2", "entry3", "entry4" };  
		private string[] stopStrings = new String[] { "stop1", "stop2", "stop3", "stop4" };
		private string[] targetStrings = new String[] { "target1", "target2", "target3", "target4" };
		private int[,] stopTargetTicks = new int[,] { {5, 4}, {3, 8}, {3, 10}, {3, 12} }; 		// Int Array of {# of stop ticks, # of target ticks}
		private string[] allPivots = new string[0];
		
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "OrderFlowPivots";
				Calculate									= Calculate.OnEachTick;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Day;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				
				
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
			} 
			else if (State == State.Configure)
			{	
				//Adds data series for daily charts
				AddDataSeries("ES 06-21", BarsPeriodType.Day, 1);
				
				//Adds data series for 1min charts to cancel unfilled orders 
				AddDataSeries("ES 06-21", BarsPeriodType.Minute, 1);
				Print("Data Series Added");
				
				// Sets the values for the prior day's high, low, and close
				if (PriorDayOHLC().PriorHigh[1] > 0) {
				    priorHigh = PriorDayOHLC().PriorHigh[1];
				    priorLow = PriorDayOHLC().PriorLow[1];
					priorClose = PriorDayOHLC().PriorClose[1];
				}
				Print("Prior HLC complete");
				//Get data for the current open
				currentOpen = PriorDayOHLC().Open[1];
				
				//Calculate the VAH and VAL of the prior day
				priorVAH = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).VAt[1];
				priorVAL = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).VAb[1];
				Print("prior value area complete");
				
				currentVAH = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).VAt[2];
				currentVAL = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).VAb[2];
				currentPOC = CalculateValueArea(false, @"VWTPO", 0.7, 8, 30, 6.75).POC[2];
				Print("current value area complete");
				
				allOrders = new Order[] { target1, target2, target3, target4 };
				stopOrders = new Order[] { stop1, stop2, stop3, stop4 };
				targetOrders = new Order[] { target1, target2, target3, target4 };
				Print("new order array complete");
				
			} 
		}
		
		protected override void OnBarUpdate() {
			
			for (int index = 0; index < allOrders.Length; index++) {
				
				/* Cancel all working orders if the price has gone 10 points above our limit order
				   or if 15 mins have passed since the pacement of our order						*/
				if (allOrders[index].OrderState == OrderState.Working || GetCurrentBid() > (allOrders[index].LimitPrice + (50 * TickSize))) {
						CancelOrder(allOrders[index]);
					Print("on bar update complete");
				}
			}
			
			
		}
		
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale) {
		  base.OnRender(chartControl, chartScale);
		  // loop through only the rendered bars on the chart
		  for(int barIndex = ChartBars.FromIndex; barIndex <= ChartBars.ToIndex; barIndex++)
		  {
		    // get the open price at the selected bar index value
		    double openPrice = Bars.GetOpen(barIndex);
			Print("On Render complete");
		  }
		}
		
		protected void OnTickUpdate() {
			
			double[] allPivots = new double[] { priorHigh, priorLow, priorClose, currentOpen, currentHigh, currentLow };
			
			/* If the current price is greater than the pivot point, add it to the supports array
			   If the current price is less than the pivot point, add it to the resistance array    */ 
			for (int index = 0; index < allPivots.Length; index++) {
				if (allPivots[index] > GetCurrentBid()) {
					Array.Resize(ref resistances, resistances.Length + 1);
					resistances[resistances.GetUpperBound(0)] = allPivots[index];
					Array.Sort(resistances, (x, y) => y.CompareTo(x));
					Print("resistances complete");
				} 
				else if (allPivots[index] < GetCurrentBid()) {
					Array.Resize(ref supports, supports.Length + 1);
					supports[supports.GetUpperBound(0)] = allPivots[index];
					Array.Sort(supports);
					Print("supports complete");
			}
		}
			
			/* Loop through the supports array and if the current price is within 12 Ticks (3 points)
			   of the support price, we are going to place a limit order at the support price and one tick below    */
			for (int index = 0; index < supports.Length; index++) {
				if ((GetCurrentBid() - supports[index]) <= (10 * TickSize) && Position.MarketPosition == MarketPosition.Flat) {
					
					//Order and Exit Target for first buy
					EnterLongLimit(1, supports[index], "entry1");
					//Order and Exit for Second Buy
					EnterLongLimit(1, supports[index] - TickSize, "entry2");
					//Order and Exits for Third Buy
					EnterLongLimit(1, supports[index] - (2 * TickSize), "entry3");
					EnterLongLimit(1, supports[index] - (2 * TickSize), "entry4");
					Print("long entries complete");
				}
			}
			
			for (int index = 0; index < supports.Length; index++) {
				if ((supports[index] - GetCurrentBid()) <= (12 * TickSize) && Position.MarketPosition == MarketPosition.Flat) {
					//Order and Exit Target for first buy
					EnterLongLimit(1, resistances[index], "entry1");
					//Order and Exit for Second Buy
					EnterLongLimit(1, resistances[index] + TickSize, "entry2");
					//Order and Exits for Third Buy
					EnterLongLimit(1, resistances[index] + (2 * TickSize), "entry3");
					EnterLongLimit(1, resistances[index] + (2 * TickSize), "entry4");
					Print("short entries complete");
					
				}
			}
			
			for (int index = 0; index < stopOrders.Length; index++) {
				/*  If we have a long position and the current price is 4 ticks in profit, raise the stop loss order to breakeven  */
				if (Position.MarketPosition == MarketPosition.Long && Close[0] >= Position.AveragePrice + (4 * TickSize)) {	
					// Checks to see if our Stop order has been submitted already
		            if (stopOrders[index] != null && stopOrders[index].StopPrice < Position.AveragePrice) {
						// Modifies stop-loss to breakeven
		                stopOrders[index] = ExitLongStopMarket(0, true, stopOrders[index].Quantity, Position.AveragePrice, stopStrings[index], entryStrings[index]);
						Print("stop order modified");
					}
				}
			}
		}
		
		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError) {
			
            // Handle entry orders here. The entry object allows us to identify that the order that is calling the OnOrderUpdate() method is the entry order.
            // Assign entryOrder in OnOrderUpdate() to ensure the assignment occurs when expected.
            // This is more reliable than assigning Order objects in OnBarUpdate, as the assignment is not gauranteed to be complete if it is referenced immediately after submitting
			for (int index = 0; index < allOrders.Length; index++) {
	            if (order.Name == entryStrings[index]) {
	                allOrders[index] = order;
	                // Reset the entryOrder object to null if order was cancelled without any fill
	                if (order.OrderState == OrderState.Cancelled && order.Filled == 0) {
	                    allOrders[index] = order;
	                    sumFilled = 0;
						Print("Order updated");
	                }
	            }
				
			}
		}

		
		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time) {	
			
			/* Monitoring OnExecution to trigger the submission of stop/longExit orders instead of OnOrderUpdate() since OnExecution() is called after OnOrderUpdate()
			   which ensures that the strategy has received the execution which is used for internal signal tracking      */
			for (int index = 0; index < allOrders.Length; index++) {
				if ( allOrders[index] != null && allOrders[index] == execution.Order) {
					if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0)) {
						
						// We sum the quantities of each execution making up the entry order
						sumFilled += execution.Quantity;
						Print("Sum of filled complete");
						/*Submit stop limit and long exit orders orders for partial fills
						  These functions handle the entries for all of our long positions */
						if (Position.MarketPosition == MarketPosition.Long) {
							if (execution.Order.OrderState == OrderState.PartFilled) {
								stopOrders[index] = ExitLongStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice - (stopTargetTicks[index, 0] * TickSize)), stopStrings[index], entryStrings[index]);
								targetOrders[index] = ExitLongLimit(execution.Order.Filled, execution.Order.AverageFillPrice - (stopTargetTicks[index, 1] * TickSize), targetStrings[index], entryStrings[index]);
							}
							//Update the exit order quantities once understate turns to filled and we have seen execution quantities match order quantities
							else if (execution.Order.OrderState == OrderState.Filled && sumFilled == execution.Order.Filled) {
								//Stop Loss Order for OrderState.Filled
								stopOrders[index] = ExitLongStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice - (stopTargetTicks[index, 0] * TickSize)), stopStrings[index], entryStrings[index]);
								targetOrders[index] = ExitLongLimit(execution.Order.Filled, execution.Order.AverageFillPrice - (stopTargetTicks[index, 1] * TickSize), targetStrings[index], entryStrings[index]);
							}
							//Reset the entryOrder object and the sumFilled counter to null / 0 after the order has been filled
							if (execution.Order.OrderState != OrderState.PartFilled && sumFilled == execution.Order.Filled) {
								allOrders[index] = null;
								sumFilled = 0;
							}
						}
						
						/*Submit stop limit and short exit orders orders for partial fills
						  These functions handle the entries for all of our short positions */
						if (Position.MarketPosition == MarketPosition.Short) {
							if (execution.Order.OrderState == OrderState.PartFilled) {
								stopOrders[index] = ExitShortStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice + (stopTargetTicks[index, 0] * TickSize)), stopStrings[index], entryStrings[index]);
								targetOrders[index] = ExitShortLimit(execution.Order.Filled, execution.Order.AverageFillPrice + (stopTargetTicks[index, 1] * TickSize), targetStrings[index], entryStrings[index]);
							}
							//Update the exit order quantities once understate turns to filled and we have seen execution quantities match order quantities
							else if (execution.Order.OrderState == OrderState.Filled && sumFilled == execution.Order.Filled) {
								//Stop Loss Order for OrderState.Filled
								stopOrders[index] = ExitShortStopLimit(execution.Order.Filled, (execution.Order.AverageFillPrice + (stopTargetTicks[index, 0] * TickSize)), stopStrings[index], entryStrings[index]);
								targetOrders[index] = ExitShortLimit(execution.Order.Filled, execution.Order.AverageFillPrice + (stopTargetTicks[index, 1] * TickSize), targetStrings[index], entryStrings[index]);
							}
							//Reset the entryOrder object and the sumFilled counter to null / 0 after the order has been filled
							if (execution.Order.OrderState != OrderState.PartFilled && sumFilled == execution.Order.Filled) {
								allOrders[index] = null;
								sumFilled = 0;
							}
						}
						
						
					}
				}
				
				/* Reset the stop orders and target orders Order objects after our position has been closed */
				if ((stopOrders[index] != null && stopOrders[index] == execution.Order) || (targetOrders[index] != null && targetOrders[index] == execution.Order)) {
					if (execution.Order.OrderState != OrderState.PartFilled || execution.Order.OrderState == OrderState.PartFilled) {
						stopOrders[index] = null;
						targetOrders[index] = null;
					}
				}
			}
		}
		
	
	
	}
}
	

