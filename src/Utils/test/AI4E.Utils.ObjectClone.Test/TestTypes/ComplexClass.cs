/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * Fast Deep Copy by Expression Trees 
 * https://www.codeproject.com/articles/1111658/fast-deep-copy-by-expression-trees-c-sharp
 * 
 * MIT License
 * 
 * Copyright (c) 2014 - 2016 Frantisek Konopecky
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

namespace AI4E.Utils.ObjectClone.Test.TestTypes
{
    [Serializable]
    public class ComplexClass : ModerateClass
    {
        public ComplexClass ThisComplexClass { get; set; }

        public Tuple<ComplexClass, ModerateClass, SimpleClass> TupleOfThis { get; protected set; }

        public Dictionary<string, SimpleClass> _sampleDictionary;

        public DerivedDictionary<string, ISimpleClass> ISampleDictionary { get; private set; }

        public ISimpleClass[,,] _iSimpleMultiDimArray;

        public SimpleClass[][][] _simpleMultiDimArray;

        public Struct[] _structArray;

        public delegate void DelegateType();

        public Delegate _justDelegate;

        public readonly Delegate _readonlyDelegate;

        public event DelegateType JustEvent;

        public bool IsJustEventNull { get { return JustEvent == null; } }

        public ComplexClass()
            : base(propertyPrivate: - 1, propertyProtected: true, fieldPrivate: "fieldPrivate_" + typeof (ComplexClass))
        {
            ThisComplexClass = this;

            TupleOfThis = new Tuple<ComplexClass, ModerateClass, SimpleClass>(this, this, this);

            _sampleDictionary = new DerivedDictionary<string, SimpleClass>();
            ISampleDictionary = new DerivedDictionary<string, ISimpleClass>();

            _justDelegate = new DelegateType(() => CreateForTests());
            _readonlyDelegate = new DelegateType(() => CreateForTests());
            JustEvent += new DelegateType(() => CreateForTests());
        }

        public static ComplexClass CreateForTests()
        {
            var complexClass = new ComplexClass();

            var dict1 = new DerivedDictionary<string, SimpleClass>();
            complexClass._sampleDictionary = dict1;

            dict1[typeof(ComplexClass).ToString()] = new ComplexClass();
            dict1[typeof(ModerateClass).ToString()] = new ModerateClass(1, true, "madeInComplexClass");
            dict1[typeof(SimpleClass).ToString()] = new SimpleClass(2, false, "madeInComplexClass");

            var dict2 = complexClass.ISampleDictionary;

            dict2[typeof (ComplexClass).ToString()] = dict1[typeof (ComplexClass).ToString()];
            dict2[typeof (ModerateClass).ToString()] = dict1[typeof (ModerateClass).ToString()];
            dict2[typeof(SimpleClass).ToString()] = new SimpleClass(2, false, "madeInComplexClass");

            var array1 = new ISimpleClass[2, 1, 1];
            array1[0,0,0] = new SimpleClass(4, false, "madeForMultiDimArray");
            array1[1,0,0] = new ComplexClass();
            complexClass._iSimpleMultiDimArray = array1;

            var array2 = new SimpleClass[2][][];
            array2[1] = new SimpleClass[2][];
            array2[1][1] = new SimpleClass[2];
            array2[1][1][1] = (SimpleClass)array1[0, 0, 0];
            complexClass._simpleMultiDimArray = array2;
            
            complexClass._structArray = new Struct[2];
            complexClass._structArray[0] = new Struct(1, complexClass, SimpleClass.CreateForTests(5));
            complexClass._structArray[1] = new Struct(3, new SimpleClass(3,false,"inStruct"), SimpleClass.CreateForTests(6));

            return complexClass;
        }
    }
}
