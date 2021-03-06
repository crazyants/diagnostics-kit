﻿/**
 *  Part of the Diagnostics Kit
 *
 *  Copyright (C) 2016  Sebastian Solnica
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.

 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 */

using LowLevelDesign.Diagnostics.Castle.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LowLevelDesign.Diagnostics.Castle.Tests
{
    public class KeywordsParserTests
    {
        [Fact]
        public void Test()
        {
            var s = "http:200.0.1 test test2 ip:192.168.0.1 bla bla2 ";
            var keywordsParsed = KeywordsParser.Parse(s);
            Assert.Equal("200.0.1", keywordsParsed.HttpStatus);
            Assert.Equal("192.168.0.1", keywordsParsed.ClientIp);
            Assert.Null(keywordsParsed.Url);
            Assert.Null(keywordsParsed.Service);
            Assert.Equal("test test2 bla bla2 ", keywordsParsed.FreeText);

            s = "http:200.0.1";
            keywordsParsed = KeywordsParser.Parse(s);
            Assert.Equal("200.0.1", keywordsParsed.HttpStatus);
            Assert.Null(keywordsParsed.ClientIp);
            Assert.Null(keywordsParsed.Url);
            Assert.Null(keywordsParsed.Service);
            Assert.Equal("", keywordsParsed.FreeText);

            s = null;
            keywordsParsed = KeywordsParser.Parse(s);
            Assert.NotNull(keywordsParsed);
            Assert.Null(keywordsParsed.Url);
            Assert.Null(keywordsParsed.Service);
            Assert.Null(keywordsParsed.HttpStatus);
            Assert.Null(keywordsParsed.ClientIp);
            Assert.Null(keywordsParsed.FreeText);

            s = "testurl:test lkjsdflksjf ghttp:sdfsf";
            keywordsParsed = KeywordsParser.Parse(s);
            Assert.NotNull(keywordsParsed);
            Assert.Null(keywordsParsed.Url);
            Assert.Null(keywordsParsed.Service);
            Assert.Null(keywordsParsed.HttpStatus);
            Assert.Null(keywordsParsed.ClientIp);
            Assert.Equal(s, keywordsParsed.FreeText);

            s = "   url:'test url with space' 1test 2test service:'service name with space' '";
            keywordsParsed = KeywordsParser.Parse(s);
            Assert.Equal("test url with space", keywordsParsed.Url);
            Assert.Equal("service name with space", keywordsParsed.Service);
            Assert.Null(keywordsParsed.HttpStatus);
            Assert.Null(keywordsParsed.ClientIp);
            Assert.Equal("   1test 2test '", keywordsParsed.FreeText);

            s = " url: service:'test' 1test2";
            keywordsParsed = KeywordsParser.Parse(s);
            Assert.Null(keywordsParsed.Url);
            Assert.Equal("test", keywordsParsed.Service);
            Assert.Null(keywordsParsed.HttpStatus);
            Assert.Null(keywordsParsed.ClientIp);
            Assert.Equal(" url: 1test2", keywordsParsed.FreeText);

            s = " url:' service:'test' 1test2";
            keywordsParsed = KeywordsParser.Parse(s);
            Assert.Equal(" service:", keywordsParsed.Url);
            Assert.Null(keywordsParsed.Service);
            Assert.Null(keywordsParsed.HttpStatus);
            Assert.Null(keywordsParsed.ClientIp);
            Assert.Equal("est' 1test2", keywordsParsed.FreeText); // TODO: we consider this valid?


            s = "test test2 url:";
            keywordsParsed = KeywordsParser.Parse(s);
            Assert.Null(keywordsParsed.Url);
            Assert.Null(keywordsParsed.Service);
            Assert.Null(keywordsParsed.HttpStatus);
            Assert.Null(keywordsParsed.ClientIp);
            Assert.Equal("test test2 url:", keywordsParsed.FreeText);

            s = "test test2 url:'";
            keywordsParsed = KeywordsParser.Parse(s);
            Assert.Null(keywordsParsed.Url);
            Assert.Null(keywordsParsed.Service);
            Assert.Null(keywordsParsed.HttpStatus);
            Assert.Null(keywordsParsed.ClientIp);
            Assert.Equal("test test2 url:'", keywordsParsed.FreeText);
        }
    }
}
