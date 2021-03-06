﻿using System;
using System.Text;

namespace FBCore.Common.Internal
{
    /// <summary>
    /// Static convenience methods that help a method or constructor check whether 
    /// it was invoked correctly (whether its <i>Preconditions</i> have been met). 
    /// These methods generally accept a <code>bool</code> expression which is 
    /// expected to be <code>true</code> (or in the case of <code>CheckNotNull</code>, 
    /// an object reference which is expected to be non-null). When <code>false</code> 
    /// (or <code>null</code>) is passed instead, the <code>Preconditions</code> 
    /// method throws an unchecked exception, which helps the calling method 
    /// communicate to <i>its</i> caller that <i>that</i> caller has made a mistake.
    /// Example:
    ///
    ///   /// Returns the positive square root of the given value.
    ///   ///
    ///   /// <exception cref="ArgumentException">If the value is negative.</exception>
    ///   <code>
    ///   public static double Sqrt(double value) 
    ///   {
    ///     Preconditions.CheckArgument(value >= 0.0, $"negative value: {0}", value);
    ///     // calculate the square root
    ///   }
    ///
    ///   void ExampleBadCaller() 
    ///   {
    ///     double d = sqrt(-1.0);
    ///   }
    ///   </code>
    ///
    /// In this example, <code>CheckArgument</code> throws an <code>ArgumentException</code>
    /// to indicate that <code>ExampleBadCaller</code> made an error in <i>its</i> call 
    /// to <code>Sqrt</code>.
    ///
    /// <h3>Warning about performance</h3>
    ///
    /// <para />The goal of this class is to improve readability of code, but in 
    /// some circumstances this may come at a significant performance cost. Remember 
    /// that parameter values for message construction must all be computed eagerly, 
    /// and autoboxing and varargs array creation may happen as well, even when the 
    /// precondition check then succeeds (as it should almost always do in production).
    /// In some circumstances these wasted CPU cycles and allocations can add up to 
    /// a real problem.
    /// Performance-sensitive precondition checks can always be converted to the 
    /// customary form:
    /// <code>
    ///   <![CDATA[ 
    ///     if (value < 0.0) 
    ///     {
    ///         throw new ArgumentException("negative value: " + value);
    ///     }
    ///   ]]>   
    /// </code>
    ///
    /// <h3>Other types of preconditions</h3>
    ///
    /// <para />Not every type of precondition failure is supported by these methods.
    ///
    /// <h3>Only <code>%s</code> is supported</h3>
    ///
    /// <para />In <code>Preconditions</code> error message template strings, 
    /// only the <code>"%s"</code> specifier is supported, not the full range of 
    /// string.Format specifiers. However, note that if the number of arguments 
    /// does not match the number of occurrences of <code>"%s"</code> in the format
    /// string, <code>Preconditions</code> will still behave as expected, and will 
    /// still include all argument values in the error message; the message will 
    /// simply not be formatted exactly as intended.
    ///
    /// <h3>More information</h3>
    ///
    /// <para />See the Guava User Guide on
    /// <a href="http://code.google.com/p/guava-libraries/wiki/PreconditionsExplained">
    /// using <code>Preconditions</code></a>.
    ///
    /// @author Kevin Bourrillion
    /// @since 2.0 (imported from Google Collections Library)
    /// </summary>
    public static class Preconditions
    {
        /// <summary>
        /// Ensures the truth of an expression involving one or more parameters 
        /// to the calling method.
        /// </summary>
        /// <param name="expression">A boolean expression.</param>
        /// <exception cref="ArgumentException">
        /// If <code>expression</code> is false.
        /// </exception>
        public static void CheckArgument(bool expression)
        {
            if (!expression)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Ensures the truth of an expression involving one or more parameters 
        /// to the calling method.
        /// </summary>
        /// <param name="expression">A boolean expression.</param>
        /// <param name="errorMessage">
        /// The exception message to use if the check fails; will be converted 
        /// to a string using <see cref="object.ToString"/>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// If <code>expression</code> is false.
        /// </exception>
        public static void CheckArgument(bool expression, object errorMessage)
        {
            if (!expression)
            {
                throw new ArgumentException(errorMessage?.ToString() ?? "null");
            }
        }

        /// <summary>
        /// Ensures the truth of an expression involving one or more parameters 
        /// to the calling method.
        /// </summary>
        /// <param name="expression">A boolean expression.</param>
        /// <param name="errorMessageTemplate">
        /// A template for the exception message should the check fail. 
        /// The message is formed by replacing each <code>%s</code> placeholder 
        /// in the template with an argument.
        /// These are matched by position - the first <code>%s</code> gets 
        /// <code>errorMessageArgs[0]</code>, etc.
        /// Unmatched arguments will be appended to the formatted message in 
        /// square braces. Unmatched placeholders will be left as-is.
        /// </param>
        /// <param name="errorMessageArgs">
        /// The arguments to be substituted into the message template.
        /// Arguments are converted to strings using <see cref="object.ToString"/>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// If <code>expression</code> is false.
        /// </exception>
        /// <exception cref="NullReferenceException">
        /// If the check fails and either <code>errorMessageTemplate</code> or
        /// <code>errorMessageArgs</code> is null (don't let this happen).
        /// </exception>
        public static void CheckArgument(
            bool expression, 
            string errorMessageTemplate, 
            params object[] errorMessageArgs)
        {
            if (!expression)
            {
                throw new ArgumentException(
                    Format(errorMessageTemplate, errorMessageArgs));
            }
        }

        /// <summary>
        /// Ensures the truth of an expression involving the state of the calling 
        /// instance, but not involving any parameters to the calling method.
        /// </summary>
        /// <param name="expression">A boolean expression.</param>
        /// <exception cref="InvalidOperationException">
        /// If <code>expression</code> is false.
        /// </exception>
        public static void CheckState(bool expression)
        {
            if (!expression)
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Ensures the truth of an expression involving the state of the calling
        /// instance, but not involving any parameters to the calling method.
        /// </summary>
        /// <param name="expression">A boolean expression.</param>
        /// <param name="errorMessage">
        /// The exception message to use if the check fails; will be converted to a
        /// string using <see cref="object.ToString"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// If <code>expression</code> is false.
        /// </exception>
        public static void CheckState(bool expression, object errorMessage)
        {
            if (!expression)
            {
                throw new InvalidOperationException(errorMessage?.ToString() ?? "null");
            }
        }

        /// <summary>
        /// Ensures the truth of an expression involving the state of the calling
        /// instance, but not involving any parameters to the calling method.
        /// </summary>
        /// <param name="expression">A boolean expression.</param>
        /// <param name="errorMessageTemplate">
        /// A template for the exception message should the check fail.
        /// The message is formed by replacing each <code>%s</code> placeholder in 
        /// the template with an argument.
        /// These are matched by position - the first <code>%s</code> gets
        /// <code>errorMessageArgs[0]</code>, etc.
        /// Unmatched arguments will be appended to the formatted message in square 
        /// braces. Unmatched placeholders will be left as-is.
        /// </param>
        /// <param name="errorMessageArgs">
        /// The arguments to be substituted into the message template. Arguments
        /// are converted to strings using <see cref="object.ToString"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// If <code>expression</code> is false.
        /// </exception>
        /// <exception cref="NullReferenceException">
        /// If the check fails and either <code>errorMessageTemplate</code> or
        /// <code>errorMessageArgs</code> is null (don't let this happen).
        /// </exception>
        public static void CheckState(
            bool expression, 
            string errorMessageTemplate, 
            params object[] errorMessageArgs)
        {
            if (!expression)
            {
                throw new InvalidOperationException(
                    Format(errorMessageTemplate, errorMessageArgs));
            }
        }

        /// <summary>
        /// Ensures that an object reference passed as a parameter to the calling
        /// method is not null.
        /// </summary>
        /// <param name="reference">An object reference.</param>
        /// <returns>The non-null reference that was validated.</returns>
        /// <exception cref="NullReferenceException">
        /// If <code>reference</code> is null.
        /// </exception>
        public static T CheckNotNull<T>(T reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            return reference;
        }

        /// <summary>
        /// Ensures that an object reference passed as a parameter to the calling
        /// method is not null.
        /// </summary>
        /// <param name="reference">An object reference.</param>
        /// <param name="errorMessage">
        /// The exception message to use if the check fails; will be converted to a
        /// string using <see cref="object.ToString"/>.
        /// </param>
        /// <returns>The non-null reference that was validated.</returns>
        /// <exception cref="NullReferenceException">
        /// If <code>reference</code> is null.
        /// </exception>
        public static T CheckNotNull<T>(T reference, object errorMessage)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(errorMessage?.ToString() ?? "null");
            }

            return reference;
        }

        /// <summary>
        /// Ensures that an object reference passed as a parameter to the calling
        /// method is not null.
        /// </summary>
        /// <param name="reference">An object reference.</param>
        /// <param name="errorMessageTemplate">
        /// A template for the exception message should the check fail.
        /// The message is formed by replacing each <code>%s</code> placeholder in 
        /// the template with an argument.
        /// These are matched by position - the first <code> %s</code>gets
        /// <code>errorMessageArgs[0]</code>, etc.
        /// Unmatched arguments will be appended to the formatted message in square
        /// braces. Unmatched placeholders will be left as-is.
        /// </param>
        /// <param name="errorMessageArgs">
        /// The arguments to be substituted into the message template. Arguments
        /// are converted to strings using <see cref="object.ToString"/>.
        /// </param>
        /// <returns>The non-null reference that was validated.</returns>
        /// <exception cref="NullReferenceException">
        /// If <code>reference</code> is null.
        /// </exception>
        public static T CheckNotNull<T>(
            T reference, 
            string errorMessageTemplate, 
            params object[] errorMessageArgs)
        {
            if (reference == null)
            {
                // If either of these parameters is null, the right thing happens anyway
                throw new ArgumentNullException(
                    Format(errorMessageTemplate, errorMessageArgs));
            }

            return reference;
        }

        /// <summary>
        /// All recent hotspots (as of 2009) *really* like to have the natural code.
        ///
        /// if (guardExpression) 
        /// {
        ///    throw new BadException(messageExpression);
        /// }
        ///
        /// Refactored so that messageExpression is moved to a separate
        /// String-returning method.
        ///
        /// if (guardExpression) 
        /// {
        ///    throw new BadException(badMsg(...));
        /// }
        ///
        /// The alternative natural refactorings into void or Exception-returning 
        /// methods are much slower. This is a big deal - we're talking factors of 2-8
        /// in microbenchmarks, not just 10-20%.
        /// (This is a hotspot optimizer bug, which should be fixed, but that's a 
        /// separate, big project).
        ///
        /// The coding pattern above is heavily used in java.util, e.g. in ArrayList.
        /// There is a RangeCheckMicroBenchmark in the JDK that was used to test this.
        ///
        /// But the methods in this class want to throw different exceptions, depending
        /// on the args, so it appears that this pattern is not directly applicable.
        /// But we can use the ridiculous, devious trick of throwing an exception in 
        /// the middle of the construction of another exception.
        /// Hotspot is fine with that.
        /// </summary>

        /// <summary>
        /// Ensures that <code>index</code> specifies a valid <i>element</i> in an array,
        /// list or string of size <code>size</code>. An element index may range from zero, 
        /// inclusive, to <code>size</code>, exclusive.
        /// </summary>
        /// <param name="index">
        /// A user-supplied index identifying an element of an array, list or string.
        /// </param>
        /// <param name="size">The size of that array, list or string.</param>
        /// <returns>The value of <code>index</code>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <code>index</code> is negative or is not less than <code>size</code>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If <code> size</code> is negative.
        /// </exception>
        public static int CheckElementIndex(int index, int size)
        {
            return CheckElementIndex(index, size, "index");
        }

        /// <summary>
        /// Ensures that <code>index</code> specifies a valid <i>element</i> in an array,
        /// list or string of size <code>size</code>. An element index may range from zero,
        /// inclusive, to <code>size</code>, exclusive.
        /// </summary>
        /// <param name="index">
        /// A user-supplied index identifying an element of an array, list or string.
        /// </param>
        /// <param name="size">The size of that array, list or string.</param>
        /// <param name="desc">
        /// The text to use to describe this index in an error message.
        /// </param>
        /// <returns>The value of <code>index</code>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <code>index</code> is negative or is not less than <code>size</code>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If <code> size</code> is negative.
        /// </exception>
        public static int CheckElementIndex(int index, int size, string desc)
        {
            // Carefully optimized for execution by hotspot (explanatory comment above)
            if (index < 0 || index >= size)
            {
                throw new ArgumentOutOfRangeException(BadElementIndex(index, size, desc));
            }

            return index;
        }

        private static string BadElementIndex(int index, int size, string desc)
        {
            if (index < 0)
            {
                return Format("%s (%s) must not be negative", desc, index);
            }
            else if (size < 0)
            {
                throw new ArgumentException("negative size: " + size);
            }
            else
            {   
                // index >= size
                return Format("%s (%s) must be less than size (%s)", desc, index, size);
            }
        }

        /// <summary>
        /// Ensures that <code>index</code> specifies a valid <i>position</i> in an array,
        /// list or string of size <code>size</code>. A position index may range from zero 
        /// to <code>size</code>, inclusive.
        /// </summary>
        /// <param name="index">
        /// A user-supplied index identifying a position in an array, list or string.
        /// </param>
        /// <param name="size">The size of that array, list or string.</param>
        /// <returns>The value of <code>index</code>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <code>index</code> is negative or is greater than <code>size</code>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If <code>size</code> is negative.
        /// </exception>
        public static int CheckPositionIndex(int index, int size)
        {
            return CheckPositionIndex(index, size, "index");
        }

        /// <summary>
        /// Ensures that <code>index</code> specifies a valid <i>position</i> in an array,
        /// list or string of size <code>size</code>. A position index may range from zero
        /// to <code>size</code>, inclusive.
        /// </summary>
        /// <param name="index">
        /// A user-supplied index identifying a position in an array, list or string.
        /// </param>
        /// <param name="size">The size of that array, list or string.</param>
        /// <param name="desc">
        /// The text to use to describe this index in an error message.
        /// </param>
        /// <returns>The value of <code>index</code>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <code>index</code> is negative or is greater than <code>size</code>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If <code> size</code> is negative.
        /// </exception>
        public static int CheckPositionIndex(int index, int size, string desc)
        {
            // Carefully optimized for execution by hotspot (explanatory comment above)
            if (index < 0 || index > size)
            {
                throw new ArgumentOutOfRangeException(BadPositionIndex(index, size, desc));
            }

            return index;
        }

        private static string BadPositionIndex(int index, int size, string desc)
        {
            if (index < 0)
            {
                return Format("%s (%s) must not be negative", desc, index);
            }
            else if (size < 0)
            {
                throw new ArgumentException("negative size: " + size);
            }
            else
            { 
                // Index > size
                return Format("%s (%s) must not be greater than size (%s)", desc, index, size);
            }
        }

        /// <summary>
        /// Ensures that <code>start</code> and <code>end</code> specify a valid
        /// <i>positions</i> in an array, list or string of size <code> size</code>,
        /// and are in order. A position index may range from zero to
        /// <code> size</code>, inclusive.
        /// </summary>
        /// <param name="start">
        /// A user-supplied index identifying a starting position in an array,
        /// list or string.
        /// </param>
        /// <param name="end">
        /// A user-supplied index identifying a ending position in an array,
        /// list or string.
        /// </param>
        /// <param name="size">The size of that array, list or string.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If either index is negative or is greater than <code>size</code>,
        /// or if <code>end</code> is less than <code> start</code>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If <code>size</code> is negative.
        /// </exception>
        public static void CheckPositionIndexes(int start, int end, int size)
        {
            // Carefully optimized for execution by hotspot (explanatory comment above)
            if (start < 0 || end < start || end > size)
            {
                throw new ArgumentOutOfRangeException(BadPositionIndexes(start, end, size));
            }
        }

        private static string BadPositionIndexes(int start, int end, int size)
        {
            if (start < 0 || start > size)
            {
                return BadPositionIndex(start, size, "start index");
            }

            if (end < 0 || end > size)
            {
                return BadPositionIndex(end, size, "end index");
            }

            // End < start
            return Format("end index (%s) must not be less than start index (%s)", end, start);
        }

        /// <summary>
        /// Substitutes each <code>%s</code> in <code>template</code> with an argument.
        /// These are matched by position: the first <code>%s</code> gets 
        /// <code>args[0]</code>, etc.
        /// If there are more arguments than placeholders, the unmatched arguments will
        /// be appended to the end of the formatted message in square braces.
        /// </summary>
        /// <param name="template">
        /// A non-null string containing 0 or more <code>%s</code> placeholders.
        /// </param>
        /// <param name="args">
        /// The arguments to be substituted into the message template.
        /// Arguments are converted to strings using <see cref="object.ToString"/>.
        /// Arguments can be null.
        /// </param>
        private static string Format(string template, params object[] args)
        {
            template = template?.ToString() ?? "null"; // null -> "null"

            // Start substituting the arguments into the '%s' placeholders
            StringBuilder builder = new StringBuilder(template.Length + 16 * args.Length);
            int templateStart = 0;
            int i = 0;
            while (i < args.Length)
            {
                int placeholderStart = template.IndexOf("%s", templateStart);
                if (placeholderStart == -1)
                {
                    break;
                }

                builder.Append(template.Substring(templateStart, placeholderStart));
                builder.Append(args[i++]);
                templateStart = placeholderStart + 2;
            }

            builder.Append(template.Substring(templateStart));

            // if we run out of placeholders, append the extra args in square braces
            if (i < args.Length)
            {
                builder.Append(" [");
                builder.Append(args[i++]);
                while (i < args.Length)
                {
                    builder.Append(", ");
                    builder.Append(args[i++]);
                }

                builder.Append(']');
            }

            return builder.ToString();
        }
    }
}
