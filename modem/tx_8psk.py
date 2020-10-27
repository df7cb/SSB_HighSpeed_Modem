#!/usr/bin/env python3
# -*- coding: utf-8 -*-

#
# SPDX-License-Identifier: GPL-3.0
#
# GNU Radio Python Flow Graph
# Title: 8PSK Modem  DJ0ABR
# Author: kurt
# Description: requires GNU Radio 3.8xxx
# GNU Radio version: 3.8.2.0

from gnuradio import analog
from gnuradio import audio
from gnuradio import blocks
from gnuradio import digital
from gnuradio import gr
from gnuradio.filter import firdes
import sys
import signal
from argparse import ArgumentParser
from gnuradio.eng_arg import eng_float, intx
from gnuradio import eng_notation


class tx_8psk(gr.top_block):

    def __init__(self, resamprate=24, samp_rate=48000):
        gr.top_block.__init__(self, "8PSK Modem  DJ0ABR")

        ##################################################
        # Parameters
        ##################################################
        self.resamprate = resamprate
        self.samp_rate = samp_rate

        ##################################################
        # Variables
        ##################################################
        self.sps = sps = 1
        self.nfilts = nfilts = 32
        self.mixf = mixf = 1500

        ##################################################
        # Blocks
        ##################################################
        self.digital_constellation_modulator_0 = digital.generic_mod(
            constellation=digital.constellation_8psk_natural().base(),
            differential=True,
            samples_per_symbol=resamprate,
            pre_diff_code=True,
            excess_bw=0.25,
            verbose=False,
            log=False)
        self.blocks_udp_source_0 = blocks.udp_source(gr.sizeof_char*1, '127.0.0.1', 40134, 258, False)
        self.blocks_multiply_xx_0_0 = blocks.multiply_vcc(1)
        self.blocks_multiply_const_vxx_0 = blocks.multiply_const_ff(0.05)
        self.blocks_complex_to_float_1 = blocks.complex_to_float(1)
        self.blocks_add_xx_0 = blocks.add_vff(1)
        self.audio_sink_0_0 = audio.sink(samp_rate, '', True)
        self.analog_sig_source_x_0_0_0 = analog.sig_source_c(samp_rate, analog.GR_COS_WAVE, mixf, 1, 0, 0)



        ##################################################
        # Connections
        ##################################################
        self.connect((self.analog_sig_source_x_0_0_0, 0), (self.blocks_multiply_xx_0_0, 1))
        self.connect((self.blocks_add_xx_0, 0), (self.blocks_multiply_const_vxx_0, 0))
        self.connect((self.blocks_complex_to_float_1, 0), (self.blocks_add_xx_0, 0))
        self.connect((self.blocks_complex_to_float_1, 1), (self.blocks_add_xx_0, 1))
        self.connect((self.blocks_multiply_const_vxx_0, 0), (self.audio_sink_0_0, 0))
        self.connect((self.blocks_multiply_xx_0_0, 0), (self.blocks_complex_to_float_1, 0))
        self.connect((self.blocks_udp_source_0, 0), (self.digital_constellation_modulator_0, 0))
        self.connect((self.digital_constellation_modulator_0, 0), (self.blocks_multiply_xx_0_0, 0))


    def get_resamprate(self):
        return self.resamprate

    def set_resamprate(self, resamprate):
        self.resamprate = resamprate

    def get_samp_rate(self):
        return self.samp_rate

    def set_samp_rate(self, samp_rate):
        self.samp_rate = samp_rate
        self.analog_sig_source_x_0_0_0.set_sampling_freq(self.samp_rate)

    def get_sps(self):
        return self.sps

    def set_sps(self, sps):
        self.sps = sps

    def get_nfilts(self):
        return self.nfilts

    def set_nfilts(self, nfilts):
        self.nfilts = nfilts

    def get_mixf(self):
        return self.mixf

    def set_mixf(self, mixf):
        self.mixf = mixf
        self.analog_sig_source_x_0_0_0.set_frequency(self.mixf)




def argument_parser():
    description = 'requires GNU Radio 3.8xxx'
    parser = ArgumentParser(description=description)
    parser.add_argument(
        "-r", "--resamprate", dest="resamprate", type=intx, default=24,
        help="Set resamprate [default=%(default)r]")
    parser.add_argument(
        "-s", "--samp-rate", dest="samp_rate", type=intx, default=48000,
        help="Set samp_rate [default=%(default)r]")
    return parser


def main(top_block_cls=tx_8psk, options=None):
    if options is None:
        options = argument_parser().parse_args()
    tb = top_block_cls(resamprate=options.resamprate, samp_rate=options.samp_rate)

    def sig_handler(sig=None, frame=None):
        tb.stop()
        tb.wait()

        sys.exit(0)

    signal.signal(signal.SIGINT, sig_handler)
    signal.signal(signal.SIGTERM, sig_handler)

    tb.start()

    tb.wait()


if __name__ == '__main__':
    main()
