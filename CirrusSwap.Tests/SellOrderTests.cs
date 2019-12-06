using Moq;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;
using static SellOrder;

namespace CirrusSwap.Tests
{
    public class SellOrderTests
    {
        private readonly Mock<ISmartContractState> MockContractState;
        private readonly Mock<IPersistentState> MockPersistentState;
        private readonly Mock<IContractLogger> MockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> MockInternalExecutor;
        private readonly Address Seller;
        private readonly Address BuyerOne;
        private readonly Address BuyerTwo;
        private readonly Address TokenAddress;
        private readonly Address ContractAddress;
        private readonly ulong TokenAmount;
        private readonly ulong TokenPrice;
        private readonly bool IsActive;

        public SellOrderTests()
        {
            MockContractLogger = new Mock<IContractLogger>();
            MockPersistentState = new Mock<IPersistentState>();
            MockContractState = new Mock<ISmartContractState>();
            MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            MockContractState.Setup(x => x.PersistentState).Returns(MockPersistentState.Object);
            MockContractState.Setup(x => x.ContractLogger).Returns(MockContractLogger.Object);
            MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            Seller = "0x0000000000000000000000000000000000000001".HexToAddress();
            BuyerOne = "0x0000000000000000000000000000000000000002".HexToAddress();
            BuyerTwo = "0x0000000000000000000000000000000000000003".HexToAddress();
            TokenAddress = "0x0000000000000000000000000000000000000004".HexToAddress();
            ContractAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
        }

        private SellOrder NewSellOrder(Address sender, ulong value, ulong price, ulong amount)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, sender, value));
            MockContractState.Setup(x => x.GetBalance).Returns(() => value);
            MockContractState.Setup(x => x.Block.Number).Returns(12345);
            MockPersistentState.Setup(x => x.GetAddress(nameof(Seller))).Returns(Seller);
            MockPersistentState.Setup(x => x.GetAddress(nameof(TokenAddress))).Returns(TokenAddress);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(TokenAmount))).Returns(amount);
            MockPersistentState.Setup(x => x.GetUInt64(nameof(TokenPrice))).Returns(price);
            MockPersistentState.Setup(x => x.GetBool(nameof(IsActive))).Returns(true);

            return new SellOrder(MockContractState.Object, TokenAddress, price, amount);
        }

        [Theory]
        [InlineData(0, 10_000_000, 5_000_000_000)]
        public void Creates_New_Trade(ulong value, ulong price, ulong amount)
        {
            var order = NewSellOrder(Seller, value, price, amount);

            MockPersistentState.Verify(x => x.SetAddress(nameof(Seller), Seller));
            Assert.Equal(Seller, order.Seller);
            
            MockPersistentState.Verify(x => x.SetAddress(nameof(TokenAddress), TokenAddress));
            Assert.Equal(TokenAddress, order.TokenAddress);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(TokenPrice), price));
            Assert.Equal(price, order.TokenPrice);

            MockPersistentState.Verify(x => x.SetUInt64(nameof(TokenAmount), amount));
            Assert.Equal(amount, order.TokenAmount);

            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), true));
            Assert.Equal(true, order.IsActive);
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(1000, 0)]
        public void Create_NewTrade_Fails_Invalid_Parameters(ulong price, ulong amount)
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, Seller, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => new SellOrder(MockContractState.Object, TokenAddress, amount, price));
        }

        [Fact]
        public void Success_GetOrderDetails()
        {
            ulong value = 0;
            ulong amount = 1;
            ulong price = 1;

            var order = NewSellOrder(Seller, value, price, amount);

            var actualOrderDetails = order.GetOrderDetails();
            var expectedOrderDetails = new OrderDetails
            {
                SellerAddress = Seller,
                TokenAddress = TokenAddress,
                TokenPrice = price,
                TokenAmount = amount,
                OrderType = nameof(SellOrder),
                IsActive = true
            };

            Assert.Equal(expectedOrderDetails, actualOrderDetails);
        }

        #region Close Trade
        [Fact]
        public void CloseOrder_Failure_If_Sender_IsNot_Owner()
        {
            var order = NewSellOrder(Seller, 0, 1, 1);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, BuyerOne, 0));

            Assert.ThrowsAny<SmartContractAssertException>(order.CloseOrder);

            MockPersistentState.Verify(x => x.GetAddress(nameof(Seller)), Times.AtLeastOnce);
        }

        [Fact]
        public void CloseOrder_Success_Sender_Is_Owner()
        {
            var order = NewSellOrder(Seller, 0, 1, 1);

            order.CloseOrder();

            MockPersistentState.Verify(x => x.GetAddress(nameof(Seller)), Times.Once);
            MockPersistentState.Verify(x => x.SetBool(nameof(IsActive), false), Times.Once);

            MockPersistentState.Setup(x => x.GetBool(nameof(IsActive))).Returns(false);

            Assert.Equal(false, order.IsActive);
        }
        #endregion
    }
}