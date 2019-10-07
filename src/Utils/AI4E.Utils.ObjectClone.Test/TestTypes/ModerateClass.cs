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

namespace AI4E.Utils.ObjectClone.Test.TestTypes
{
    [Serializable]
    public class ModerateClass : SimpleClass
    {
        public string PropertyPublic2 { get; set; }

        protected bool PropertyProtected2 { get; set; }

        public int _fieldPublic2;

        private int PropertyPrivate { get; set; }

#pragma warning disable IDE0044
        private string _fieldPrivate;
#pragma warning restore IDE0044

        public Struct _structField;

        public DeeperStruct _deeperStructField;
        
        public GenericClass<SimpleClass> _genericClassField; 

        public SimpleClass SimpleClassProperty { get; set; }

        public SimpleClass _readonlySimpleClassField;

        public SimpleClass[] SimpleClassArray { get; set; }

        public object ObjectTextProperty { get; set; }

        public ModerateClass(int propertyPrivate, bool propertyProtected, string fieldPrivate)
            : base(propertyPrivate, propertyProtected, fieldPrivate)
        {
            PropertyPrivate = propertyPrivate + 1;
            _fieldPrivate = fieldPrivate + "_" + typeof(ModerateClass);
            ObjectTextProperty = "Test";
        }

        public static new ModerateClass CreateForTests(int seed)
        {
            var moderateClass = new ModerateClass(seed, seed % 2 == 1, "seed_" + seed)
            {
                _fieldPublic = seed,
                _fieldPublic2 = seed + 1
            };

            moderateClass._structField = new Struct(seed, moderateClass, SimpleClass.CreateForTests(seed));
            moderateClass._deeperStructField = new DeeperStruct(seed, SimpleClass.CreateForTests(seed));

            moderateClass._genericClassField = new GenericClass<SimpleClass>(moderateClass, SimpleClass.CreateForTests(seed));

            var seedSimple = seed + 1000;

            moderateClass.SimpleClassProperty = new SimpleClass(seedSimple, seed % 2 == 1, "seed_" + seedSimple);

            moderateClass._readonlySimpleClassField = new SimpleClass(seedSimple + 1, seed % 2 == 1, "seed_" + (seedSimple + 1));

            moderateClass.SimpleClassArray = new SimpleClass[10];

            for (var i = 1; i <= 10; i++)
            {
                moderateClass.SimpleClassArray[i - 1] = new SimpleClass(seedSimple + i, seed % 2 == 1, "seed_" + (seedSimple + i));
            }

            return moderateClass;
        }

        public int GetPrivateProperty2()
        {
            return PropertyPrivate;
        }

        public string GetPrivateField2()
        {
            return _fieldPrivate;
        }
    }
}
