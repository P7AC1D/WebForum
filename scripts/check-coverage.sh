#!/bin/bash
# Coverage Quality Gate Script
# This script checks coverage thresholds and provides actionable feedback

COVERAGE_FILE="${1:-coverage-report/Summary.json}"
MIN_LINE_COVERAGE="${2:-80}"
MIN_BRANCH_COVERAGE="${3:-70}"
MIN_METHOD_COVERAGE="${4:-80}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

if [ ! -f "$COVERAGE_FILE" ]; then
    echo -e "${RED}❌ Coverage file not found: $COVERAGE_FILE${NC}"
    exit 1
fi

# Check if jq is available
if ! command -v jq &> /dev/null; then
    echo -e "${RED}❌ jq is required but not installed${NC}"
    exit 1
fi

# Parse coverage data with error handling
LINE_COVERAGE=$(jq -r '.summary.linecoverage // 0' "$COVERAGE_FILE" 2>/dev/null)
BRANCH_COVERAGE=$(jq -r '.summary.branchcoverage // 0' "$COVERAGE_FILE" 2>/dev/null)
METHOD_COVERAGE=$(jq -r '.summary.methodcoverage // 0' "$COVERAGE_FILE" 2>/dev/null)

# Validate parsed values
if [ "$LINE_COVERAGE" = "null" ] || [ -z "$LINE_COVERAGE" ]; then LINE_COVERAGE=0; fi
if [ "$BRANCH_COVERAGE" = "null" ] || [ -z "$BRANCH_COVERAGE" ]; then BRANCH_COVERAGE=0; fi
if [ "$METHOD_COVERAGE" = "null" ] || [ -z "$METHOD_COVERAGE" ]; then METHOD_COVERAGE=0; fi

echo -e "${CYAN}📊 Coverage Analysis Results:${NC}"
echo "├── Line Coverage:   ${LINE_COVERAGE}% (target: ${MIN_LINE_COVERAGE}%)"
echo "├── Branch Coverage: ${BRANCH_COVERAGE}% (target: ${MIN_BRANCH_COVERAGE}%)"
echo "└── Method Coverage: ${METHOD_COVERAGE}% (target: ${MIN_METHOD_COVERAGE}%)"
echo ""

# Check thresholds
PASS=true

if (( $(echo "$LINE_COVERAGE < $MIN_LINE_COVERAGE" | bc -l) )); then
    echo -e "${RED}❌ Line coverage below threshold: ${LINE_COVERAGE}% < ${MIN_LINE_COVERAGE}%${NC}"
    PASS=false
fi

if (( $(echo "$BRANCH_COVERAGE < $MIN_BRANCH_COVERAGE" | bc -l) )); then
    echo -e "${RED}❌ Branch coverage below threshold: ${BRANCH_COVERAGE}% < ${MIN_BRANCH_COVERAGE}%${NC}"
    PASS=false
fi

if (( $(echo "$METHOD_COVERAGE < $MIN_METHOD_COVERAGE" | bc -l) )); then
    echo -e "${RED}❌ Method coverage below threshold: ${METHOD_COVERAGE}% < ${MIN_METHOD_COVERAGE}%${NC}"
    PASS=false
fi

if [ "$PASS" = true ]; then
    echo -e "${GREEN}✅ All coverage thresholds met!${NC}"
    echo ""
    echo -e "${YELLOW}🛡️ Security Services Priority Check:${NC}"
    echo "Verify that Tier 1 services (SecurityService, AuthService, SanitizationService) have high coverage"
    
    # Display top covered classes if coverage data exists
    echo ""
    echo -e "${CYAN}🎯 Top Coverage by Class:${NC}"
    jq -r '.coverage[]? | select(.name? and .linecoverage?) | [.name, .linecoverage] | @tsv' "$COVERAGE_FILE" 2>/dev/null | \
    sort -k2 -nr | head -5 | while IFS=$'\t' read -r name coverage; do
        if (( $(echo "$coverage >= 90" | bc -l) )); then
            echo "🟢 $name: ${coverage}%"
        elif (( $(echo "$coverage >= 80" | bc -l) )); then
            echo "🟡 $name: ${coverage}%"
        else
            echo "🔴 $name: ${coverage}%"
        fi
    done
    
    exit 0
else
    echo ""
    echo -e "${YELLOW}💡 Improvement Suggestions:${NC}"
    echo "1. Focus on Tier 1 Security Services first"
    echo "2. Add edge case tests for business logic"
    echo "3. Test error handling paths"
    echo "4. Consider property-based testing for complex logic"
    echo ""
    echo -e "${YELLOW}🔍 Classes needing attention:${NC}"
    jq -r '.coverage[]? | select(.name? and .linecoverage? and (.linecoverage < 80)) | [.name, .linecoverage] | @tsv' "$COVERAGE_FILE" 2>/dev/null | \
    sort -k2 -n | while IFS=$'\t' read -r name coverage; do
        echo -e "${RED}📉 $name: ${coverage}%${NC}"
    done
    
    exit 1
fi
