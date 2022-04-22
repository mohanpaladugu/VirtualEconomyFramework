﻿using System;
using System.Collections.Generic;
using System.Text;
using VEDriversLite;
using Xunit;

namespace VEFrameworkUnitTest.DogeCoin
{
    public class ValidateDogeAddressTest
    {
        /// <summary>
        /// Unit test method to verify if system is returning an error result when PrivateKey is empty.
        /// </summary>
        [Fact]
        public void ValidateDogeAddress_Empty_Test()
        {
            var key = DogeTransactionHelpers.ValidateDogeAddress("");
            Assert.False(key.Success);
            Assert.Empty(key.Value.ToString());
        }

        /// <summary>
        /// Unit test method to verify if system is returning an error result when PrivateKey length is less than 52 characters.
        /// </summary>
        [Fact]
        public void ValidateDogeAddress_LessThan34_Test()
        {
            var key1 = DogeTransactionHelpers.ValidateDogeAddress("Test");
            Assert.False(key1.Success);
            Assert.Empty(key1.Value.ToString());
        }

        /// <summary>
        /// Unit test method to verify if system is returning an error result when PrivateKey length is less than 52 characters.
        /// </summary>
        [Fact]
        public void ValidateDogeAddress_FirstLetterNotD_Test()
        {
            var key1 = DogeTransactionHelpers.ValidateDogeAddress("TestTestTestTestTestTestTestTestTestTest");
            Assert.False(key1.Success);
            Assert.Empty(key1.Value.ToString());
        }

        /// <summary>
        /// Unit test method to verify if system is returning the bitcoin secret object correctly for a valid privateKey.
        /// </summary>
        [Fact]
        public void ValidateDogeAddress_Valid_Test()
        {
            string address = "NPvfpRCmDNcJjCZvDuAB9QsFC32gVThWdh";

            var result = DogeTransactionHelpers.ValidateDogeAddress(address);
            Assert.Equal(address, result.Value.ToString());
        }
    }
}
