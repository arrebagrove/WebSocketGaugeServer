using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.IO;

namespace FUELTRIP_Logger
{
    public abstract class FuelTripCalculatorBase
    {
        /// <summary>
        /// Current speed
        /// </summary>
        public double CurrentSpeed
        {
            public get;
            protected set;
        }

        /// <summary>
        /// Trip scaling factor
        /// </summary>
        public readonly double TripCoefficient = 1.0;
        /// <summary>
        /// Gas consumption scaling factor
        /// </summary>
        public double GasConsumptionCoefficient = 1.0;

        // Stopwatch class
        protected readonly Stopwatch StopWatch;
        protected const long STOPWATCH_TIMEOUT = 3000;        

        /// <summary>
        /// Interval time to store cumlative trip and gas consumption into file.
        /// </summary>
        private const int saveSpan = 5000;

        /// <summary>
        /// Elapsed time since last file save
        /// </summary>
        private double saveElapsed;


        /// <summary>
        ///  File path to store cumlative trip/gas data
        /// </summary>
        private readonly string filePath;

        /// <summary>
        /// Folder path to store cumlative trip/gas data
        /// </summary>
        private readonly string folderPath;

        /// <summary>
        /// Interval time to store section gas and trip.
        /// </summary>
        private long _sect_span;
        private long _sect_elapsed;
        // Queue to store section gas/trip data
        private int _sect_store_max;
        private Trip_gas_Content _sect_trip_gas_temporary;
        private Trip_gas_Content _sect_trip_gas_latest;
        private Queue<Trip_gas_Content> _sect_trip_gas_queue;

        /// <summary>
        /// Number of store to save section gas/trip data.
        /// </summary>
        public int SectStoreMax
        {
            get
            {
                return _sect_store_max;
            }
            set
            {
                _sect_store_max = value;
                reset_sect_trip_gas();
            }
        }

        /// <summary>
        /// Eventhandler called when fuel/trip data is updated.
        /// </summary>
        public event EventHandler SectFUELTRIPUpdated;

        /// <summary>
        /// Interval to store section gas/milage data.
        /// </summary>
        public long Sect_Span
        {
            get
            {
                return _sect_span;
            }
            set
            {
                _sect_span = value;
            }
        }

        //総燃料消費量、トリップ
        private Trip_gas_Content _total_trip_gas;
        public double Total_Trip
        {
            get
            {
                return _total_trip_gas.trip;
            }
        }
        public double Total_Gas_Consumption
        {
            get
            {
                return _total_trip_gas.gas_consumption;
            }
        }

        //総区間燃費(計算)
        public double Total_Gas_Milage
        {
            get
            {
                return _total_trip_gas.gas_milage;

            }
        }

        //区間トリップ、燃料消費量
        public Trip_gas_Content[] Sect_trip_gas_Array
        {
            get
            {
                Trip_gas_Content[] sect_trip_gas_array = _sect_trip_gas_queue.ToArray();
                return sect_trip_gas_array;
            }
        }

        public double[] Sect_trip_array
        {
            get
            {
                int i;
                Trip_gas_Content[] sect_trip_gas_array = _sect_trip_gas_queue.ToArray();

                double[] trip_array = new double[sect_trip_gas_array.Length];

                for (i = 0; i < sect_trip_gas_array.Length; i++)
                {
                    trip_array[i] = sect_trip_gas_array[i].trip;
                }

                return trip_array;
            }
        }

        public double[] Sect_gas_array
        {
            get
            {
                int i;
                Trip_gas_Content[] sect_trip_gas_array = _sect_trip_gas_queue.ToArray();

                double[] gas_array = new double[sect_trip_gas_array.Length];

                for (i = 0; i < sect_trip_gas_array.Length; i++)
                {
                    gas_array[i] = sect_trip_gas_array[i].gas_consumption;
                }

                return gas_array;
            }
        }

        public double[] Sect_gasmilage_array
        {
            get
            {
                int i;
                Trip_gas_Content[] sect_trip_gas_array = _sect_trip_gas_queue.ToArray();

                double[] gasmilage_array = new double[sect_trip_gas_array.Length];

                for (i = 0; i < sect_trip_gas_array.Length; i++)
                {
                    gasmilage_array[i] = sect_trip_gas_array[i].gas_milage;
                }

                return gasmilage_array;
            }
        }


        public double Sect_Trip_Latest
        {
            get
            {
                return _sect_trip_gas_latest.trip;
            }
        }
        public double Sect_Gas_Consumption_Latest
        {
            get
            {
                return _sect_trip_gas_latest.gas_consumption;
            }
        }
        public double Sect_Gas_Milage_Latest
        {
            get
            {
                return _sect_trip_gas_latest.gas_milage;
            }
        }

        //瞬間トリップ、燃料消費量
        private double _momentary_trip;
        private double _momentary_gas_consumption;
        public double Momentary_Trip
        {
            get
            {
                return _momentary_trip;
            }
        }
        public double Momentary_Gas_Consumption
        {
            get
            {
                return _momentary_gas_consumption;
            }
        }
        public double Momentary_Gas_Milage
        {
            get
            {
                if (_momentary_gas_consumption != 0)
                {
                    return _momentary_trip / _momentary_gas_consumption;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public FuelTripCalculatorBase()
        {
            _total_trip_gas = new Trip_gas_Content();

            _sect_elapsed = 0;
            _sect_store_max = 60;
            _sect_span = 60 * 1000;
            _sect_trip_gas_temporary = new Trip_gas_Content();
            _sect_trip_gas_queue = new Queue<Trip_gas_Content>();
            _sect_trip_gas_latest = new Trip_gas_Content();

            saveElapsed = 0;

            //データ格納しているファイルパスの指定
            folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            filePath = Path.Combine(folderPath, "." + "FUELTRIP_Logger");

            load_trip_gas();

            StopWatch = new Stopwatch();
            StopWatch.Reset();
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~FuelTripCalculatorBase()
        {
            save_trip_gas();
        }
        private double get_momentary_trip(long elasped_millisecond)
        {
            double speed = CurrentSpeed;

            double monentary_trip = TripCoefficient * (speed) / 3600 / 1000 * elasped_millisecond;

            return monentary_trip;
        }

        private void enqueue_sect_trip_gas(Trip_gas_Content content)
        {
            _sect_trip_gas_latest = content;
            _sect_trip_gas_queue.Enqueue(content);

            if (_sect_trip_gas_queue.Count > _sect_store_max)
            {
                _sect_trip_gas_queue.Dequeue();
            }

        }

        public void reset_total_trip_gas()
        {
            _total_trip_gas = new Trip_gas_Content();
        }

        public void reset_sect_trip_gas()
        {
            _sect_trip_gas_latest = new Trip_gas_Content();
            _sect_trip_gas_queue = new Queue<Trip_gas_Content>();
        }

        public void load_trip_gas()
        {
            //XmlSerializerオブジェクトの作成
            System.Xml.Serialization.XmlSerializer serializer =
                new System.Xml.Serialization.XmlSerializer(typeof(Trip_gas_Content));

            try
            {
                //ファイルを開く
                System.IO.FileStream fs =
                    new System.IO.FileStream(filePath, System.IO.FileMode.Open);

                try
                {
                    //XMLファイルから読み込み、逆シリアル化する
                    _total_trip_gas =
                        (Trip_gas_Content)serializer.Deserialize(fs);

                }
                catch (XmlException ex)
                {
                    Console.WriteLine(ex.Message);
                    this.reset_total_trip_gas();
                }

                fs.Close();

            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
                this.reset_total_trip_gas();
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
                System.IO.Directory.CreateDirectory(folderPath);
                this.reset_sect_trip_gas();
            }
            catch (System.Security.SecurityException ex)
            {
                Console.WriteLine(ex.Message);
                this.reset_total_trip_gas();
            }
        }


        public void save_trip_gas()
        {
            //XmlSerializerオブジェクトを作成
            //書き込むオブジェクトの型を指定する
            System.Xml.Serialization.XmlSerializer serializer1 =
                new System.Xml.Serialization.XmlSerializer(typeof(Trip_gas_Content));
            //ファイルを開く
            System.IO.FileStream fs1 =
                new System.IO.FileStream(filePath, System.IO.FileMode.Create);
            //シリアル化し、XMLファイルに保存する
            serializer1.Serialize(fs1, _total_trip_gas);
            //閉じる
            fs1.Close();
        }
    }

    public class Trip_gas_Content
    {
        public double trip { get; set; }
        public double gas_consumption { get; set; }

        public Trip_gas_Content()
        {
            reset();
        }

        private void reset()
        {
            this.trip = 0;
            this.gas_consumption = 0;
        }

        public double gas_milage
        {
            get
            {
                if (this.gas_consumption == 0)
                    return 0;
                else
                    return this.trip / this.gas_consumption;
            }
        }

    }
}
