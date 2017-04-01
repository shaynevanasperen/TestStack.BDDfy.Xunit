[![Build status](https://ci.appveyor.com/api/projects/status/n3kt7x472ma4riqj?svg=true)](https://ci.appveyor.com/project/shaynevanasperen/teststack-bddfy-xunit)
![License](https://img.shields.io/github/license/shaynevanasperen/teststack.bddfy.xunit.svg)
![Version](https://img.shields.io/nuget/v/teststack.bddfy.xunit.svg)

## TestStack.BDDfy.Xunit

This library makes it possible to run BDDfy tests using Xunit parallel test execution without causing all the
BDDfy reporting to become garbled due to it writing to the Console.

Simply use the provided `BddfyFactAttribute` or `BddfyTheoryAttribute` to mark your test methods
(instead of the normal `FactAttribute` or `TheoryAttribute` from Xunit).

```cs
[BddfyFact]
public void Fact()
{
    this.BDDfy();
}

[BddfyTheory]
[InlineData(2, 2)]
[InlineData(3, 3)]
public void Theory(int first, int second)
{
    Calculator calculator = null;
    var sum = 0;
    this.Given(() => calculator = new Calculator(), "a calculator")
        .When(() => sum = calculator.Add(first, second), "adding two integers")
        .Then(() => sum.Should().Be(first + second), "the sum is correct")
        .BDDfy();
}
```