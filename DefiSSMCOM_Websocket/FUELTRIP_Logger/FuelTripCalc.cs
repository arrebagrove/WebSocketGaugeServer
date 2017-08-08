using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;


namespace FUELTRIP_Logger
{
	public class FuelTripCalculator : FuelTripCalculatorBase
	{
		private double _current_tacho;
		private double _current_speed;
		private double _current_injpulse_width;

		//気筒数,インジェクタ関連定数
		private const int num_cylinder = 4;
		private const double injection_latency = 0.76; //無効噴射時間ms
		private const double injetcer_capacity = 575;

		//コンストラクタ
		public FuelTripCalculator() : base()
		{
		}

		public void update(double tacho, double speed, double injpulse_width)
		{
            if (!StopWatch.IsRunning)
            {
                StopWatch.Start();
                return;
            }

            StopWatch.Stop();

            //get elasped time
            long stopwatch_elasped = StopWatch.ElapsedMilliseconds;

			// Set current value
			_current_speed = speed;
			_current_tacho = tacho;
			_current_injpulse_width = injpulse_width;

			// elaspedが長すぎる場合,タイムアウトを発生
            if (stopwatch_elasped > STOPWATCH_TIMEOUT) {
				StopWatch.Reset ();
                StopWatch.Start();
				throw new TimeoutException ("tacho/speed/injpulse update span is too large (Timeout).");
			}
            StopWatch.Reset();
			StopWatch.Start ();

			_momentary_trip = get_momentary_trip(stopwatch_elasped);
			_momentary_gas_consumption = get_momentary_gas_comsumption(stopwatch_elasped);

			_total_trip_gas.trip += _momentary_trip;
			_total_trip_gas.gas_consumption += _momentary_gas_consumption;

			_sect_elapsed += stopwatch_elasped;

			//区間データアップデート
			if (_sect_elapsed < _sect_span)
			{
				_sect_trip_gas_temporary.trip += _momentary_trip;
				_sect_trip_gas_temporary.gas_consumption += _momentary_gas_consumption;
			}
			else
			{
				//Section履歴に追加
				enqueue_sect_trip_gas (_sect_trip_gas_temporary);

				_sect_trip_gas_temporary = new Trip_gas_Content();
				_sect_elapsed = 0;

				//区間データ更新イベント発生
				SectFUELTRIPUpdated (this, EventArgs.Empty);
			}

			//総燃費、総距離を5sごとに保存
			if (_save_elapsed < save_span)
			{
				_save_elapsed += stopwatch_elasped;
			}
			else
			{
				save_trip_gas();
				_save_elapsed = 0;
			}				
		}

		private double get_momentary_trip(long elasped_millisecond)
		{
			double speed = _current_speed;

			double monentary_trip = trip_coefficient * (speed) / 3600 / 1000 * elasped_millisecond;

			return monentary_trip;
		}

		private double get_momentary_gas_comsumption(long elasped_millisecond)
		{
			//燃料消費量計算
			double inj_pulse_width = _current_injpulse_width;
			double tacho = _current_tacho;

			double momentary_gas_consumption;
			if (tacho > 500) //アイドリング回転以下の場合は、燃料消費量に加えない
				momentary_gas_consumption = gas_consumption_coefficient * (double)num_cylinder * (double)tacho * injetcer_capacity * (inj_pulse_width - injection_latency) / (7.2E9) * elasped_millisecond / 1000;
			else
				momentary_gas_consumption = 0;

            //燃料消費量が負の場合、燃料消費量は0とする
            if (momentary_gas_consumption < 0)
                momentary_gas_consumption = 0;

			return momentary_gas_consumption;
		}
	}

	public class Trip_gas_Content
	{
		public double trip { get; set; }
		public double gas_consumption { get; set; }

		public Trip_gas_Content()
		{
			reset ();
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

