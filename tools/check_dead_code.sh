#!/bin/bash
# Dead Code Detection Script for Monster Gambit
# Usage: ./check_dead_code.sh

echo "=== Monster Gambit Dead Code Checker ==="
echo

# Check for common dead code patterns
echo "Checking for potential dead code patterns..."
echo

# 1. Empty methods (excluding constructors and interface implementations)
echo "1. Empty Methods:"
grep -n "^\s*{[\s]*}[\s]*$" *.cs 2>/dev/null | head -5
if [ $? -ne 0 ]; then
    echo "   No obvious empty methods found."
fi
echo

# 2. Methods marked as "TODO" or "placeholder"
echo "2. Placeholder/TODO Methods:"
grep -ni "todo\|placeholder\|fixme" *.cs 2>/dev/null | head -5
if [ $? -ne 0 ]; then
    echo "   No obvious placeholder methods found."
fi
echo

# 3. Unused using statements (basic check)
echo "3. Potentially Unused Using Statements:"
for file in *.cs; do
    if [ -f "$file" ]; then
        echo "   Checking $file:"
        # Extract using statements
        grep "^using " "$file" | while read line; do
            namespace=$(echo "$line" | sed 's/using //; s/;//' | tr -d ' ')
            simple_name=$(echo "$namespace" | sed 's/.*\.//')
            
            # Check if namespace or simple name is used in the file
            if ! grep -q "$simple_name" "$file" && ! grep -q "$namespace" "$file"; then
                echo "     Potentially unused: $line"
            fi
        done
    fi
done
echo

# 4. Methods with only Task.Delay or empty implementation
echo "4. Methods with Minimal Implementation:"
grep -A 5 -B 1 "Task\.Delay\|await Task\.Delay" *.cs 2>/dev/null | head -10
if [ $? -ne 0 ]; then
    echo "   No methods with Task.Delay found."
fi
echo

# 5. Commented out code blocks
echo "5. Large Commented Code Blocks:"
grep -n "^\s*//.*{" *.cs 2>/dev/null | head -5
if [ $? -ne 0 ]; then
    echo "   No large commented code blocks found."
fi
echo

# 6. Potentially unused constants
echo "6. Potentially Unused Constants:"
grep -n "const.*=" *.cs 2>/dev/null | while read line; do
    const_name=$(echo "$line" | sed 's/.*const[^=]*\([A-Za-z_][A-Za-z0-9_]*\).*/\1/')
    file=$(echo "$line" | cut -d: -f1)
    line_num=$(echo "$line" | cut -d: -f2)
    
    # Count occurrences (should be more than 1 if used)
    count=$(grep -c "$const_name" "$file")
    if [ "$count" -eq 1 ]; then
        echo "   $file:$line_num - Constant '$const_name' may be unused"
    fi
done
echo

echo "=== Analysis Complete ==="
echo "Note: This is a basic analysis. Manual review is recommended."
echo "For more thorough analysis, consider using:"
echo "  - Visual Studio's Code Analysis"
echo "  - Roslyn Analyzers"
echo "  - SonarQube"
echo "  - Resharper (if available)"