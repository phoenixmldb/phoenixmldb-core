# Acknowledgements

PhoenixML builds on the work of many individuals and organizations. We gratefully
acknowledge their contributions.

## W3C Specifications

PhoenixML implements specifications developed by the World Wide Web Consortium (W3C)
and the Qt4 Community Group:

- **XQuery 3.1 / 4.0** — W3C Recommendation / Qt4 Community Group Draft
- **XPath 3.1 / 4.0** — W3C Recommendation / Qt4 Community Group Draft
- **XSLT 3.0 / 4.0** — W3C Recommendation / Qt4 Community Group Draft
- **XQuery and XPath Data Model (XDM) 3.1 / 4.0**
- **XPath and XQuery Functions and Operators 3.1 / 4.0**
- **XSLT and XQuery Serialization 3.1 / 4.0**
- **XQuery Update Facility 3.0**
- **XQuery and XPath Full Text 3.0**

These specifications are the product of years of work by the W3C XSL Working Group,
the W3C XML Query Working Group, and the Qt4 Community Group. We thank all editors
and contributors to these specifications.

## W3C XSLT 3.0 Test Suite

PhoenixML validates conformance against the
[W3C XSLT 3.0 Test Suite](https://github.com/w3c/xslt30-test), which is used under
the [W3C Test Suite License](https://www.w3.org/Consortium/Legal/2008/04-testsuite-copyright.html).

The test suite was produced using the following XQuery command against the test data:

```
xquery 'sort(distinct-values(collection()//*:created/@by))' tests/
```

### Primary Authors & Editors

- **Michael Kay** (Saxonica)
- **Debbie Lockett** (Saxonica)
- **Abel Braaksma**

### Test Contributors

- Charles Foster
- Colin Adams
- David Rudel
- John Lumley
- Norman Walsh
- O'Neil Delpratt (Saxonica)
- Scott Boag
- Toshihito Makita

### Contributors via Bug Reports & Test Ideas

- Andy Yar
- Christian Roth
- Claudio Sacerdoti Coen
- Dak Tapaal
- Dave Haffner
- Dave Pawson
- David Marston
- David Maus
- Frank Steimke
- Geoff Crowther
- Jiri Dolejsi
- Joel Kalvesmaki
- Julian Reschke
- Ken Holman
- Marcus Lauer
- Mark Dunn
- Martin Honnen
- Max Toro
- Michael Wirth
- Morten Jorgensen
- Norm Tovey-Walsh
- Paul Dick
- Phil Fearon
- Rohit Gaikwad
- Ruud Grossmann
- T. Hatanaka
- Tim Mills
- Tom Hillman
- Vladimir Nestorovsky
- Wendell Piez

## Open Source Dependencies

PhoenixML uses the following open source libraries:

| Library | License | Usage |
|---------|---------|-------|
| [ANTLR 4](https://www.antlr.org/) | BSD-3-Clause | XQuery/XPath parser generation |
| [Lucene.NET](https://lucenenet.apache.org/) | Apache-2.0 | Full-text search indexing and analysis |

## Tools

- [.NET](https://dotnet.microsoft.com/) — Runtime and SDK (MIT License)
- [xUnit](https://xunit.net/) — Test framework (Apache-2.0)
