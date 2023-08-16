using CryptoBlade.Models;

namespace CryptoBlade.BackTesting
{
    public class OpenPositionWithOrders
    {
        private readonly List<Order> m_filledOrders;
        private Position m_position;

        public OpenPositionWithOrders(Order filledOrder)
        {
            m_filledOrders = new List<Order>
            {
                filledOrder
            };
            m_position = CalculatePosition(filledOrder, null, filledOrder);
        }

        protected OpenPositionWithOrders(List<Order> filledOrders, Position position, decimal unrealizedProfitOrLoss)
        {
            m_filledOrders = filledOrders;
            m_position = position;
            UnrealizedProfitOrLoss = unrealizedProfitOrLoss;
        }

        public Position Position => m_position;

        public decimal UnrealizedProfitOrLoss { get; private set; }

        public IReadOnlyList<Order> FilledOrders => m_filledOrders;

        public void UpdateUnrealizedProfitOrLoss(Candle candle)
        {
            if (Position.Side == PositionSide.Buy)
                UnrealizedProfitOrLoss = (candle.Open - Position.AveragePrice) * Position.Quantity;
            else if (Position.Side == PositionSide.Sell)
                UnrealizedProfitOrLoss = (Position.AveragePrice - candle.Open) * Position.Quantity;
        }

        public void AddOrder(Order order)
        {
            m_filledOrders.Add(order);
            m_position = CalculatePosition(order, m_position, m_filledOrders.First());
        }

        private static Position CalculatePosition(Order order, Position? existingPosition, Order firstOrder)
        {
            Position position;
            if (order.ReduceOnly!.Value)
            {
                position = new Position
                {
                    Symbol = firstOrder.Symbol,
                    Side = firstOrder.Side == OrderSide.Buy ? PositionSide.Buy : PositionSide.Sell,
                    TradeMode = TradeMode.CrossMargin,
                    Quantity = existingPosition!.Quantity - order.Quantity,
                    AveragePrice = existingPosition.AveragePrice,
                };
            }
            else
            {
                decimal positionQuantity = existingPosition?.Quantity ?? 0;
                decimal positionAveragePrice = existingPosition?.AveragePrice ?? 0;
                var totalQuantity = positionQuantity + order.Quantity;
                var averagePrice = (positionAveragePrice * positionQuantity + order.Price!.Value * order.Quantity)
                                   / (totalQuantity);
                position = new Position
                {
                    Symbol = firstOrder.Symbol,
                    Side = firstOrder.Side == OrderSide.Buy ? PositionSide.Buy : PositionSide.Sell,
                    TradeMode = TradeMode.CrossMargin,
                    Quantity = totalQuantity,
                    AveragePrice = averagePrice,
                };
            }

            return position;
        }

        public OpenPositionWithOrders Clone()
        {
            return new OpenPositionWithOrders(m_filledOrders.Select(x => x.Clone()).ToList(), m_position.Clone(),
                               UnrealizedProfitOrLoss);
        }
    }
}