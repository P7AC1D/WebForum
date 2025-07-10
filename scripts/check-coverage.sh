#!/bin/bash

# Coverage Quality Gate Script
# This script checks coverage thresholds and provides actionable feedback

COVERAGE_FILE="coverage-report/Summary.json"
MIN_LINE_COVERAGE=80
MIN_BRANCH_COVERAGE=70
MIN_METHOD_COVERAGE=80

if [ ! -f "$COVERAGE_FILE" ]; then
    echo "❌ Coverage file not found: $COVERAGE_FILE"
    exit 1
fi

# Extract coverage metrics
LINE_COVERAGE=$(cat "$COVERAGE_FILE" | jq -r '.summary.linecoverage')
BRANCH_COVERAGE=$(cat "$COVERAGE_FILE" | jq -r '.summary.branchcoverage') 
METHOD_COVERAGE=$(cat "$COVERAGE_FILE" | jq -r '.summary.methodcoverage')

echo "📊 Coverage Analysis Results:"
echo "├── Line Coverage:   ${LINE_COVERAGE}% (target: ${MIN_LINE_COVERAGE}%)"
echo "├── Branch Coverage: ${BRANCH_COVERAGE}% (target: ${MIN_BRANCH_COVERAGE}%)"
echo "└── Method Coverage: ${METHOD_COVERAGE}% (target: ${MIN_METHOD_COVERAGE}%)"
echo

# Check thresholds
PASS=true

if (( $(echo "$LINE_COVERAGE < $MIN_LINE_COVERAGE" | bc -l) )); then
    echo "❌ Line coverage below threshold: ${LINE_COVERAGE}% < ${MIN_LINE_COVERAGE}%"
    PASS=false
fi

if (( $(echo "$BRANCH_COVERAGE < $MIN_BRANCH_COVERAGE" | bc -l) )); then
    echo "❌ Branch coverage below threshold: ${BRANCH_COVERAGE}% < ${MIN_BRANCH_COVERAGE}%"
    PASS=false
fi

if (( $(echo "$METHOD_COVERAGE < $MIN_METHOD_COVERAGE" | bc -l) )); then
    echo "❌ Method coverage below threshold: ${METHOD_COVERAGE}% < ${MIN_METHOD_COVERAGE}%"
    PASS=false
fi

if [ "$PASS" = true ]; then
    echo "✅ All coverage thresholds met!"
    echo
    echo "🛡️ Security Services Priority Check:"
    echo "Verify that Tier 1 services (SecurityService, AuthService, SanitizationService) have high coverage"
    exit 0
else
    echo
    echo "💡 Improvement Suggestions:"
    echo "1. Focus on Tier 1 Security Services first"
    echo "2. Add edge case tests for business logic"
    echo "3. Test error handling paths"
    echo "4. Consider property-based testing for complex logic"
    exit 1
fi
